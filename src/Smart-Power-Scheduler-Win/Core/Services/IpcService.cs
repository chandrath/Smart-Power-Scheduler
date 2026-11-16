using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SmartPowerScheduler.Core.Models;

namespace SmartPowerScheduler.Core.Services;

/// <summary>
/// Lightweight, zero-firewall local Named Pipe IPC service
/// to forward command-line arguments and broadcast live timer state
/// to companion integrations like Elgato Stream Deck.
/// </summary>
public static class IpcService
{
    public const string PipeName = "SmartPowerScheduler_IPC_Pipe_9E2F9F24";
    public const string StatePipeName = "SmartPowerScheduler_State_Pipe_9E2F9F24";

    private static readonly List<NamedPipeServerStream> _stateClients = new();
    private static readonly object _stateLock = new();
    private static TimerStateSnapshot _lastSnapshot = TimerStateSnapshot.Stopped();

    public static bool TrySendToExistingInstance(string[] args)
    {
        if (args == null || args.Length == 0) return false;

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(350); // Fast 350ms timeout

            string payload = string.Join("\u001F", args);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);

            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void StartServer(Action<string[]> onArgumentsReceived, CancellationToken ct = default)
    {
        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct);

                    using var ms = new MemoryStream();
                    byte[] buffer = new byte[1024];
                    int bytesRead;

                    while ((bytesRead = await server.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                        if (bytesRead < buffer.Length) break;
                    }

                    string payload = Encoding.UTF8.GetString(ms.ToArray());
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        string[] args = payload.Split('\u001F', StringSplitOptions.RemoveEmptyEntries);
                        onArgumentsReceived(args);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Delay before retry in case of pipe reset
                    try { await Task.Delay(500, ct); } catch { break; }
                }
            }
        }, ct);
    }

    public static void BroadcastState(TimerStateSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        string json = JsonSerializer.Serialize(snapshot) + "\n";
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        lock (_stateLock)
        {
            for (int i = _stateClients.Count - 1; i >= 0; i--)
            {
                var client = _stateClients[i];
                if (!client.IsConnected)
                {
                    try { client.Dispose(); } catch { }
                    _stateClients.RemoveAt(i);
                    continue;
                }

                try
                {
                    client.Write(bytes, 0, bytes.Length);
                    client.Flush();
                }
                catch
                {
                    try { client.Dispose(); } catch { }
                    _stateClients.RemoveAt(i);
                }
            }
        }
    }

    public static void StartStateServer(CancellationToken ct = default)
    {
        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        StatePipeName,
                        PipeDirection.Out,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct);

                    // Send initial state immediately upon connection
                    string json = JsonSerializer.Serialize(_lastSnapshot) + "\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await server.WriteAsync(bytes, 0, bytes.Length, ct);
                    await server.FlushAsync(ct);

                    lock (_stateLock)
                    {
                        _stateClients.Add(server);
                    }
                }
                catch (OperationCanceledException)
                {
                    server?.Dispose();
                    break;
                }
                catch
                {
                    server?.Dispose();
                    try { await Task.Delay(500, ct); } catch { break; }
                }
            }
        }, ct);
    }
}
