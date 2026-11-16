using System.IO;
using System.Text;
using System.Text.Json;
using SmartPowerScheduler.Config;
using SmartPowerScheduler.Core.Models;

namespace SmartPowerScheduler.Core.Services;

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

    public static void ReconcilePendingStartupSessions()
    {
        lock (FileLock)
        {
            try
            {
                var list = LoadRecordsInternal();
                bool modified = false;
                DateTime systemBootTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);

                foreach (var record in list)
                {
                    if (record.WakeUpTimestamp != null) continue; // Already calculated and frozen permanently

                    if (record.Action is PowerAction.Shutdown or PowerAction.ForceShutdown or PowerAction.Restart or PowerAction.ForceRestart or PowerAction.Hibernate)
                    {
                        if (systemBootTime > record.Timestamp)
                        {
                            record.WakeUpTimestamp = systemBootTime;
                            record.Status = "Completed";
                            modified = true;
                        }
                    }
                }

                if (modified) SaveRecordsInternal(list);
            }
            catch { }
        }
    }

    public static bool DeleteRecord(string recordId)
    {
        lock (FileLock)
        {
            try
            {
                var list = LoadRecordsInternal();
                int removed = list.RemoveAll(r => r.Id == recordId);
                if (removed > 0)
                {
                    SaveRecordsInternal(list);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }

    public static void LogSessionFailed(string recordId, string errorReason)
    {
        lock (FileLock)
        {
            try
            {
                var list = LoadRecordsInternal();
                var record = list.FirstOrDefault(r => r.Id == recordId);
                if (record != null)
                {
                    record.Status = "Failed";
                    SaveRecordsInternal(list);
                }
            }
            catch { }
        }
    }

    public static void ExportHistory(string destinationPath)
    {
        lock (FileLock)
        {
            var list = LoadRecordsInternal();
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(list, options);
            File.WriteAllText(destinationPath, json);
        }
    }

    public static void ImportHistory(string sourcePath, out int addedCount, out int skippedCount)
    {
        addedCount = 0;
        skippedCount = 0;

        string json = File.ReadAllText(sourcePath);
        var incoming = JsonSerializer.Deserialize<List<SessionRecord>>(json);
        if (incoming == null || incoming.Count == 0) return;

        lock (FileLock)
        {
            var existing = LoadRecordsInternal();
            foreach (var inc in incoming)
            {
                var match = existing.FirstOrDefault(e =>
                    e.Id == inc.Id ||
                    (e.Action == inc.Action && Math.Abs((e.Timestamp - inc.Timestamp).TotalSeconds) < 15));

                if (match == null)
                {
                    existing.Add(inc);
                    addedCount++;
                }
                else
                {
                    skippedCount++;
                    if (match.WakeUpTimestamp == null && inc.WakeUpTimestamp != null)
                    {
                        match.WakeUpTimestamp = inc.WakeUpTimestamp;
                        match.Status = inc.Status;
                    }
                }
            }

            existing = existing.OrderByDescending(r => r.Timestamp).ToList();

            int max = AppConfig.Instance.Defaults.MaxHistoryRecords > 0 ? AppConfig.Instance.Defaults.MaxHistoryRecords : 100000;
            if (existing.Count > max) existing.RemoveRange(max, existing.Count - max);

            SaveRecordsInternal(existing);
        }
    }

    public static void GetAnalytics(out int total, out int thisMonth, out int thisYear, out string totalSleepTime)
    {
        var list = GetHistory();
        total = list.Count;
        var now = DateTime.Now;
        thisMonth = list.Count(r => r.Timestamp.Year == now.Year && r.Timestamp.Month == now.Month);
        thisYear = list.Count(r => r.Timestamp.Year == now.Year);

        double totalHours = 0;
        foreach (var r in list)
        {
            if (r.WakeUpTimestamp != null && r.WakeUpTimestamp > r.Timestamp)
                totalHours += (r.WakeUpTimestamp.Value - r.Timestamp).TotalHours;
        }

        if (totalHours >= 24)
        {
            int days = (int)(totalHours / 24);
            int h = (int)(totalHours % 24);
            totalSleepTime = h > 0 ? $"{days}d {h}h" : $"{days}d";
        }
        else if (totalHours >= 1)
        {
            int h = (int)totalHours;
            int m = (int)((totalHours - h) * 60);
            totalSleepTime = m > 0 ? $"{h}h {m}m" : $"{h}h";
        }
        else if (totalHours > 0)
        {
            totalSleepTime = $"{Math.Max(1, (int)(totalHours * 60))}m";
        }
        else
        {
            totalSleepTime = "0m";
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
        sb.AppendLine("⚡ Smart Power Scheduler — Session Stats");
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
        sb.AppendLine("Get Smart Power Scheduler for Windows:");
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

