using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartSleepScheduler.Core.Models;

namespace SmartSleepScheduler.StreamDeck;

public static class Program
{
    private const string CommandPipeName = "SmartSleepScheduler_IPC_Pipe_9E2F9F24";
    private const string StatePipeName = "SmartSleepScheduler_State_Pipe_9E2F9F24";

    private static readonly ConcurrentDictionary<string, KeyConfig> _keys = new();
    private static ClientWebSocket? _ws;
    private static TimerStateSnapshot _lastState = TimerStateSnapshot.Stopped();
    private static string? _cachedExePath;
    private static readonly Dictionary<string, string> _imageBase64 = new(StringComparer.OrdinalIgnoreCase);

    public static async Task Main(string[] args)
    {
        Log($"Started with args: {string.Join(" ", args)}");
        PreloadImageAssets();

        int port = 0;
        string? pluginUuid = null;
        string? registerEvent = null;

        for (int i = 0; i < args.Length; i++)
        {
            string flag = args[i].ToLowerInvariant();
            if (flag == "-port" && i + 1 < args.Length) int.TryParse(args[++i], out port);
            else if (flag == "-pluginuuid" && i + 1 < args.Length) pluginUuid = args[++i];
            else if (flag == "-registerevent" && i + 1 < args.Length) registerEvent = args[++i];
        }

        Log($"Parsed: port={port}, pluginUuid={pluginUuid}, registerEvent={registerEvent}");

        if (port == 0 || string.IsNullOrEmpty(pluginUuid) || string.IsNullOrEmpty(registerEvent))
        {
            Log("Missing required arguments. Exiting.");
            return;
        }

        using var cts = new CancellationTokenSource();

        // 1. Start IPC State Pipe Monitor in Background
        _ = Task.Run(() => MonitorAppStateAsync(cts.Token));

        // 2. Connect to Elgato Stream Deck WebSocket Host
        try
        {
            _ws = new ClientWebSocket();
            var uri = new Uri($"ws://127.0.0.1:{port}");
            Log($"Connecting to WebSocket {uri}...");
            await _ws.ConnectAsync(uri, cts.Token);
            Log("WebSocket connected.");

            var regPayload = JsonSerializer.Serialize(new { @event = registerEvent, uuid = pluginUuid });
            Log($"Sending register payload: {regPayload}");
            await SendWebSocketMessageAsync(regPayload, cts.Token);

            Log("Processing WebSocket events...");
            await ProcessWebSocketEventsAsync(_ws, cts.Token);
            Log("WebSocket event loop ended.");
        }
        catch (Exception ex)
        {
            Log($"Exception in Main: {ex}");
        }
    }

    private static void Log(string msg)
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Elgato", "StreamDeck", "logs", "SmartSleepScheduler.StreamDeck.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    private static async Task ProcessWebSocketEventsAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log("WebSocket received Close frame.");
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                    return;
                }
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            string json = Encoding.UTF8.GetString(ms.ToArray());
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                var doc = JsonNode.Parse(json);
                if (doc == null) continue;

                string? evt = doc["event"]?.GetValue<string>();
                string? context = doc["context"]?.GetValue<string>();
                if (string.IsNullOrEmpty(context)) continue;

                var payload = doc["payload"];
                Log($"Stream Deck event: '{evt}' for context: {context}");

                switch (evt)
                {
                    case "willAppear":
                        HandleWillAppear(context, payload);
                        break;

                    case "willDisappear":
                        _keys.TryRemove(context, out _);
                        break;

                    case "didReceiveSettings":
                        HandleDidReceiveSettings(context, payload);
                        break;

                    case "titleParametersDidChange":
                        HandleTitleParametersDidChange(context, payload);
                        break;

                    case "keyDown":
                        await HandleKeyDownAsync(context, ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Error processing message: {ex.Message}");
            }
        }
    }

    private static void HandleWillAppear(string context, JsonNode? payload)
    {
        var cfg = ParseKeyConfig(payload?["settings"]);
        if (payload?["title"]?.GetValue<string>() is { } t && !string.IsNullOrWhiteSpace(t)) cfg.CustomTitle = t;
        _keys[context] = cfg;
        Log($"WillAppear context={context}, action={cfg.Action}, duration={cfg.Duration}");
        UpdateKeyDisplay(context, cfg, _lastState);
    }

    private static void HandleDidReceiveSettings(string context, JsonNode? payload)
    {
        var cfg = ParseKeyConfig(payload?["settings"]);
        if (_keys.TryGetValue(context, out var existing) && !string.IsNullOrWhiteSpace(existing.CustomTitle)) cfg.CustomTitle = existing.CustomTitle;
        _keys[context] = cfg;
        Log($"DidReceiveSettings context={context}, action={cfg.Action}, duration={cfg.Duration}");
        UpdateKeyDisplay(context, cfg, _lastState);
    }

    private static void HandleTitleParametersDidChange(string context, JsonNode? payload)
    {
        if (!_keys.TryGetValue(context, out var cfg)) return;
        cfg.CustomTitle = payload?["title"]?.GetValue<string>();
        UpdateKeyDisplay(context, cfg, _lastState);
    }

    private static KeyConfig ParseKeyConfig(JsonNode? settings)
    {
        var cfg = new KeyConfig();
        if (settings == null) return cfg;

        string? action = settings["action"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(action)) cfg.Action = action;

        if (settings["duration"] != null)
        {
            string d = settings["duration"]!.ToString().Trim();
            if (!string.IsNullOrEmpty(d))
            {
                d = char.IsDigit(d[^1]) ? $"{d}m" : d;
                cfg.Duration = ClampDurationString(d);
            }
        }

        if (settings["sound"] != null && bool.TryParse(settings["sound"]!.ToString(), out bool snd))
            cfg.SoundEnabled = snd;

        return cfg;
    }

    private static async Task HandleKeyDownAsync(string context, CancellationToken ct)
    {
        if (!_keys.TryGetValue(context, out var config)) config = new KeyConfig();
        Log($"KeyDown context={context}, Action={config.Action}, Duration={config.Duration}, CurrentState={_lastState.State}");

        // Case A: Timer is already running -> pressing button STOPS the countdown
        if (_lastState.State == "Running")
        {
            Log("Active countdown running -> sending STOP command to app.");
            SendCommandToApp(["--stop"]);
            return;
        }

        // Case B: Timer is stopped -> start requested action
        string soundArg = config.SoundEnabled ? "on" : "off";
        string[] startArgs = ["-a", config.Action.ToLowerInvariant(), "-t", config.Duration, "-s", soundArg, "--start"];

        Log($"Attempting to send start command to running instance: {string.Join(" ", startArgs)}");
        if (!SendCommandToApp(startArgs))
        {
            Log("App not responding on pipe. Launching app process directly...");
            LaunchApp(startArgs);
        }
    }

    private static bool SendCommandToApp(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", CommandPipeName, PipeDirection.Out);
            client.Connect(350); // 350ms quick handshake
            string payload = string.Join("\u001F", args);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            Log($"Successfully sent args via named pipe: {payload}");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Named pipe send failed: {ex.Message}");
            return false;
        }
    }

    private static void LaunchApp(string[] args)
    {
        string? exePath = FindAppExecutable();
        Log($"FindAppExecutable found: '{exePath}'");
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            Log("ERROR: SmartSleepScheduler.exe not found on system!");
            return;
        }

        try
        {
            string arguments = string.Join(" ", args);
            Log($"Launching '{exePath}' with args: '{arguments}'");
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = true
            };
            var proc = Process.Start(psi);
            Log($"Process launched successfully! PID={proc?.Id}");
        }
        catch (Exception ex)
        {
            Log($"Process launch failed: {ex.Message}");
        }
    }

    private static string? FindAppExecutable()
    {
        if (!string.IsNullOrEmpty(_cachedExePath) && File.Exists(_cachedExePath))
            return _cachedExePath;

        // 1. Check if SmartSleepScheduler is already running
        try
        {
            var running = Process.GetProcessesByName("SmartSleepScheduler").FirstOrDefault();
            if (running != null && running.MainModule?.FileName is { } rPath && File.Exists(rPath))
            {
                _cachedExePath = rPath;
                return rPath;
            }
        }
        catch { }

        // 2. Check cached app_path.txt from %APPDATA%\Appsphinx\SmartSleepScheduler
        try
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Appsphinx", "SmartSleepScheduler", "app_path.txt");
            if (File.Exists(appData))
            {
                string p = File.ReadAllText(appData).Trim();
                if (File.Exists(p)) { _cachedExePath = p; return p; }
            }
        }
        catch { }

        // 3. Check Windows Registry uninstall keys (set by Inno Setup)
        string[] registryKeys =
        [
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{9E2F9F24-B1F6-4A0B-88EE-C2A8389DB67B}_is1",
            @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{9E2F9F24-B1F6-4A0B-88EE-C2A8389DB67B}_is1"
        ];

        foreach (var subKey in registryKeys)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(subKey) ??
                                Microsoft.Win32.Registry.LocalMachine.OpenSubKey(subKey);
                if (key?.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
                {
                    string candidate = Path.Combine(loc, "SmartSleepScheduler.exe");
                    if (File.Exists(candidate)) { _cachedExePath = candidate; return candidate; }
                }
            }
            catch { }
        }

        // 4. Standard install directories & development repo builds
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "SmartSleepScheduler.exe"),
            Path.Combine(baseDir, "..", "SmartSleepScheduler.exe"),
            Path.Combine(baseDir, "..", "..", "SmartSleepScheduler.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Appsphinx", "Smart Sleep Scheduler", "SmartSleepScheduler.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Appsphinx", "Smart Sleep Scheduler", "SmartSleepScheduler.exe"),
            @"C:\GitHub\_2026\Smart-Sleep-Scheduler\src\Smart-Sleep-Scheduler-Win\bin\Release\net10.0-windows\SmartSleepScheduler.exe"
        ];

        foreach (var c in candidates)
        {
            try
            {
                string full = Path.GetFullPath(c);
                if (File.Exists(full)) { _cachedExePath = full; return full; }
            }
            catch { }
        }

        return null;
    }

    private static async Task MonitorAppStateAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", StatePipeName, PipeDirection.In);
                await pipeClient.ConnectAsync(1500, ct);
                Log("Connected to app state pipe.");

                using var reader = new StreamReader(pipeClient, Encoding.UTF8);
                while (!ct.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(ct);
                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var snapshot = JsonSerializer.Deserialize<TimerStateSnapshot>(line);
                    if (snapshot != null)
                    {
                        _lastState = snapshot;
                        UpdateAllKeysDisplay();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (_lastState.State != "Stopped")
                {
                    _lastState = TimerStateSnapshot.Stopped();
                    UpdateAllKeysDisplay();
                }

                try { await Task.Delay(1500, ct); } catch { break; }
            }
        }
    }

    private static void PreloadImageAssets()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates = [Path.Combine(baseDir, "images"), Path.Combine(baseDir, "com.appsphinx.smartsleepscheduler.sdPlugin", "images")];
        string? imgDir = candidates.FirstOrDefault(Directory.Exists);
        if (imgDir == null) return;
        foreach (var name in (string[])["idle_sleep", "idle_shutdown", "running_sleep", "running_shutdown"])
        {
            string p = Path.Combine(imgDir, $"{name}.png");
            if (File.Exists(p)) try { _imageBase64[name] = Convert.ToBase64String(File.ReadAllBytes(p)); } catch { }
        }
    }

    private static void UpdateAllKeysDisplay()
    {
        foreach (var kvp in _keys) UpdateKeyDisplay(kvp.Key, kvp.Value, _lastState);
    }

    private static void UpdateKeyDisplay(string context, KeyConfig config, TimerStateSnapshot state)
    {
        string actShort = FormatActionShort(config.Action);
        string title;
        string imgKey;

        if (state.State == "Running")
        {
            bool isPower = state.Action.Contains("shut", StringComparison.OrdinalIgnoreCase) || state.Action.Contains("restart", StringComparison.OrdinalIgnoreCase);
            imgKey = isPower ? "running_shutdown" : "running_sleep";

            bool isMatchingAction = string.Equals(config.Action, state.Action, StringComparison.OrdinalIgnoreCase);
            if (isMatchingAction || _keys.Count == 1)
            {
                string targetAct = FormatActionShort(state.Action);
                title = $"{FormatDuration(state.RemainingSeconds)}\n{targetAct}";
            }
            else
            {
                title = $"{config.Duration}\n{actShort}";
            }
        }
        else
        {
            bool isPower = config.Action.Contains("shut", StringComparison.OrdinalIgnoreCase) || config.Action.Contains("restart", StringComparison.OrdinalIgnoreCase);
            imgKey = isPower ? "idle_shutdown" : "idle_sleep";
            title = state.State == "Paused" ? $"PAUSED\n{actShort}" : (!string.IsNullOrWhiteSpace(config.CustomTitle) ? $"{config.CustomTitle}\n{config.Duration}" : $"{config.Duration}\n{actShort}");
        }

        if (config.LastAppliedImageKey != imgKey && _imageBase64.TryGetValue(imgKey, out var b64))
        {
            config.LastAppliedImageKey = imgKey;
            SetImage(context, b64);
        }

        SetTitle(context, title);
    }

    private static string ClampDurationString(string raw)
    {
        int sec = 0;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(raw, @"(\d+)\s*([hms])", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            if (int.TryParse(m.Groups[1].Value, out int v)) sec += m.Groups[2].Value.ToLowerInvariant() switch { "h" => Math.Min(v, 12) * 3600, "m" => v * 60, _ => v };
        if (sec <= 0) sec = 30;
        if (sec > 43200) sec = 43200;
        return (sec >= 3600 && sec % 3600 == 0) ? $"{sec / 3600}h" : ((sec >= 60 && sec % 60 == 0) ? $"{sec / 60}m" : $"{sec}s");
    }

    private static string FormatActionShort(string action)
    {
        string c = action.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
        return c switch { "sleep" => "SLEEP", "hibernate" => "HIBERN", "shutdown" => "SHUTDOWN", "restart" => "RESTART", "forceshutdown" => "F-SHUT", "forcerestart" => "F-RESTART", _ => action.ToUpperInvariant() };
    }

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds >= 3600) return $"{totalSeconds / 3600}h {(totalSeconds % 3600) / 60}m";
        if (totalSeconds >= 60) return $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
        return $"{Math.Max(1, totalSeconds)}s";
    }

    private static void SetTitle(string context, string title)
    {
        if (_ws?.State == WebSocketState.Open)
            _ = SendWebSocketMessageAsync(JsonSerializer.Serialize(new { @event = "setTitle", context, payload = new { title, target = 0 } }), CancellationToken.None);
    }

    private static void SetImage(string context, string base64)
    {
        if (_ws?.State == WebSocketState.Open)
            _ = SendWebSocketMessageAsync(JsonSerializer.Serialize(new { @event = "setImage", context, payload = new { image = $"data:image/png;base64,{base64}", target = 0 } }), CancellationToken.None);
    }

    private static async Task SendWebSocketMessageAsync(string json, CancellationToken ct)
    {
        if (_ws?.State != WebSocketState.Open) return;
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        try { await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct); } catch { }
    }
}

public sealed class KeyConfig
{
    public string Action { get; set; } = "Sleep";
    public string Duration { get; set; } = "30s";
    public bool SoundEnabled { get; set; } = true;
    public string? CustomTitle { get; set; }
    public string? LastAppliedImageKey { get; set; }
}
