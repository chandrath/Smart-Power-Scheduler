using System.IO;
using System.Text;
using System.Text.Json;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core.Models;

namespace SmartSleepScheduler.Core.Services;

/// <summary>
/// Lightweight, disk-backed session history service.
/// Zero idle RAM overhead: loads strictly on-demand from disk.
/// </summary>
public static class HistoryService
{
    private static readonly object FileLock = new();

    private static string HistoryFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConfig.Instance.App.Vendor,
        AppConfig.Instance.App.Name,
        "history.json"
    );

    public static string LogSessionTriggered(PowerAction action, TimeSpan duration)
    {
        var record = new SessionRecord
        {
            Action = action,
            ScheduledDurationSeconds = (int)Math.Max(1, duration.TotalSeconds),
            Status = (action == PowerAction.Sleep) ? "Initiated" : "Executed"
        };

        lock (FileLock)
        {
            try
            {
                var list = LoadRecordsInternal();
                list.Insert(0, record);

                int max = AppConfig.Instance.Defaults.MaxHistoryRecords > 0 ? AppConfig.Instance.Defaults.MaxHistoryRecords : 100;
                if (list.Count > max) list.RemoveRange(max, list.Count - max);

                SaveRecordsInternal(list);
            }
            catch
            {
                // Never crash caller during power execution
            }
        }

        return record.Id;
    }

    public static void LogSessionResumed(DateTime wakeTime)
    {
        lock (FileLock)
        {
            try
            {
                var list = LoadRecordsInternal();
                var pending = list.FirstOrDefault(r => r.Action == PowerAction.Sleep && r.Status == "Initiated");
                if (pending != null)
                {
                    pending.WakeUpTimestamp = wakeTime;
                    pending.Status = "Completed";
                    SaveRecordsInternal(list);
                }
            }
            catch
            {
                // Non-blocking resume handler
            }
        }
    }

    public static List<SessionRecord> GetHistory()
    {
        lock (FileLock)
        {
            try
            {
                return LoadRecordsInternal();
            }
            catch
            {
                return new List<SessionRecord>();
            }
        }
    }

    public static int GetTotalCount()
    {
        lock (FileLock)
        {
            try
            {
                return LoadRecordsInternal().Count;
            }
            catch
            {
                return 0;
            }
        }
    }

    public static void ClearHistory()
    {
        lock (FileLock)
        {
            try
            {
                string path = HistoryFilePath;
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Safe ignore
            }
        }
    }

    public static string GenerateShareText()
    {
        var list = GetHistory();
        int total = list.Count;
        var latest = list.FirstOrDefault();

        var sb = new StringBuilder();
        sb.AppendLine("🌙 Smart Sleep Scheduler — Session Stats");
        sb.AppendLine($"📊 Total Scheduled Sessions: {total}");

        if (latest != null)
        {
            sb.AppendLine($"🕒 Latest Action: {latest.ActionDisplayName} ({latest.FormattedDuration})");
            sb.AppendLine($"📅 Time: {latest.FormattedDate}");
            if (latest.HasSleepDuration)
            {
                sb.AppendLine($"💤 {latest.SleepDurationText}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Get Smart Sleep Scheduler for Windows:");
        sb.AppendLine(AppConfig.Instance.App.RepositoryUrl);
        return sb.ToString().TrimEnd();
    }

    private static List<SessionRecord> LoadRecordsInternal()
    {
        string path = HistoryFilePath;
        if (!File.Exists(path)) return new List<SessionRecord>();

        string json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return new List<SessionRecord>();

        return JsonSerializer.Deserialize<List<SessionRecord>>(json) ?? new List<SessionRecord>();
    }

    private static void SaveRecordsInternal(List<SessionRecord> records)
    {
        string path = HistoryFilePath;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(records, options);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(bytes, 0, bytes.Length);
        fs.Flush(flushToDisk: true);
    }
}

