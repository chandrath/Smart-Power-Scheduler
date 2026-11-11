using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SmartSleepScheduler.Core.Models;

namespace SmartSleepScheduler.Core.Services;

/// <summary>
/// Robust parser and generator for command-line arguments and shortcut flags.
/// </summary>
public static class CommandLineService
{
    private static readonly Regex DurationRegex = new(@"^(?:(\d+(?:\.\d+)?)\s*h(?:ours?|rs?)?)?\s*(?:(\d+(?:\.\d+)?)\s*m(?:in(?:ute)?s?)?)?\s*(?:(\d+)\s*s(?:ec(?:ond)?s?)?)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LaunchOverrides Parse(string[] args)
    {
        var overrides = new LaunchOverrides();
        if (args == null || args.Length == 0) return overrides;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].Trim();
            if (string.IsNullOrEmpty(arg)) continue;

            string key = arg.ToLowerInvariant();
            string? value = (i + 1 < args.Length && !args[i + 1].StartsWith('-') && !args[i + 1].StartsWith('/')) ? args[++i] : null;

            if (key.StartsWith("/action:") || key.StartsWith("--action="))
            {
                value = key.Substring(key.IndexOfAny(new[] { ':', '=' }) + 1);
                key = "--action";
            }
            else if (key.StartsWith("/time:") || key.StartsWith("--time=") || key.StartsWith("--duration="))
            {
                value = key.Substring(key.IndexOfAny(new[] { ':', '=' }) + 1);
                key = "--time";
            }
            else if (key.StartsWith("/sound:") || key.StartsWith("--sound="))
            {
                value = key.Substring(key.IndexOfAny(new[] { ':', '=' }) + 1);
                key = "--sound";
            }

            switch (key)
            {
                case "-a":
                case "--action":
                    if (value != null && TryParseAction(value, out var action))
                        overrides.Action = action;
                    break;

                case "-t":
                case "-d":
                case "--time":
                case "--duration":
                    if (value != null && TryParseDuration(value, out var duration))
                        overrides.Duration = duration;
                    break;

                case "-s":
                case "--sound":
                    if (value != null)
                    {
                        string valLower = value.ToLowerInvariant();
                        if (valLower is "off" or "false" or "0" or "mute" or "silent") overrides.SoundEnabled = false;
                        else if (valLower is "on" or "true" or "1") overrides.SoundEnabled = true;
                    }
                    break;

                case "--mute":
                case "--silent":
                    overrides.SoundEnabled = false;
                    break;

                case "-v":
                case "--volume":
                    if (value != null && int.TryParse(value, out int vol))
                        overrides.SoundVolume = Math.Clamp(vol, 10, 100);
                    break;

                case "--start":
                case "--run":
                    overrides.AutoStart = true;
                    break;

                case "--no-start":
                case "--no-autostart":
                    overrides.AutoStart = false;
                    break;
            }
        }

        return overrides;
    }

    public static bool TryParseDuration(string input, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string clean = input.Trim().ToLowerInvariant();

        // 1. Raw integer seconds check (e.g. "1800" or "60")
        if (int.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rawSecs) && rawSecs > 0)
        {
            duration = TimeSpan.FromSeconds(Math.Min(rawSecs, 43200));
            return true;
        }

        // 2. Fractional hours check (e.g. "1.5h" or "0.5h")
        if (clean.EndsWith("h") && double.TryParse(clean[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double hours) && hours > 0)
        {
            duration = TimeSpan.FromHours(Math.Min(hours, 12));
            return true;
        }

        // 3. Composite pattern check (e.g. "1h 30m", "45m", "15m 30s")
        var match = DurationRegex.Match(clean);
        if (match.Success)
        {
            double totalSeconds = 0;
            if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
                totalSeconds += h * 3600;
            if (double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double m))
                totalSeconds += m * 60;
            if (int.TryParse(match.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int s))
                totalSeconds += s;

            if (totalSeconds > 0)
            {
                duration = TimeSpan.FromSeconds(Math.Clamp((int)totalSeconds, 1, 43200));
                return true;
            }
        }

        return false;
    }

    public static bool TryParseAction(string input, out PowerAction action)
    {
        string clean = input.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "");
        action = clean switch
        {
            "sleep" or "standby" => PowerAction.Sleep,
            "hibernate" => PowerAction.Hibernate,
            "shutdown" or "poweroff" => PowerAction.Shutdown,
            "restart" or "reboot" => PowerAction.Restart,
            "forceshutdown" => PowerAction.ForceShutdown,
            "forcerestart" => PowerAction.ForceRestart,
            _ => PowerAction.Sleep
        };
        return true;
    }

    public static string GenerateArguments(PowerAction action, TimeSpan duration, bool sound)
    {
        var sb = new StringBuilder();
        string actStr = action switch
        {
            PowerAction.Sleep => "sleep",
            PowerAction.Hibernate => "hibernate",
            PowerAction.Shutdown => "shutdown",
            PowerAction.Restart => "restart",
            PowerAction.ForceShutdown => "force-shutdown",
            PowerAction.ForceRestart => "force-restart",
            _ => "sleep"
        };

        sb.Append($"-a {actStr}");

        if (duration.TotalHours >= 1)
        {
            int h = (int)duration.TotalHours;
            int m = duration.Minutes;
            sb.Append(m > 0 ? $" -t {h}h{m}m" : $" -t {h}h");
        }
        else if (duration.TotalMinutes >= 1)
        {
            int m = (int)duration.TotalMinutes;
            int s = duration.Seconds;
            sb.Append(s > 0 ? $" -t {m}m{s}s" : $" -t {m}m");
        }
        else
        {
            sb.Append($" -t {Math.Max(1, (int)duration.TotalSeconds)}s");
        }

        sb.Append(sound ? " -s on" : " -s off");

        return sb.ToString();
    }
}

