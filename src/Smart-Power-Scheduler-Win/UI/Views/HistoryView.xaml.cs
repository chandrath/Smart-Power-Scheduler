using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SmartPowerScheduler.Core;
using SmartPowerScheduler.Core.Models;
using SmartPowerScheduler.Core.Services;

namespace SmartPowerScheduler.UI.Views;

public partial class HistoryView : UserControl
{
    public event Action? Closed;

    private List<SessionRecord> _allRecords = new();
    private int _currentLimit = 50;

    public HistoryView()
    {
        InitializeComponent();
    }

    public void RefreshData()
    {
        HistoryService.GetAnalytics(out int total, out int thisMonth, out int thisYear, out string sleepTime);
        StatTotalText.Text = total.ToString();
        StatMonthText.Text = thisMonth.ToString();
        StatYearText.Text = thisYear.ToString();
        StatSleepTimeText.Text = sleepTime;

        _allRecords = HistoryService.GetHistory();
        _currentLimit = 50;
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        if (FilterCombo == null || SortCombo == null || RecordsListBox == null) return;

        var now = DateTime.Now;
        IEnumerable<SessionRecord> filtered = FilterCombo.SelectedIndex switch
        {
            1 => _allRecords.Where(r => r.Action == PowerAction.Sleep),
            2 => _allRecords.Where(r => r.Action == PowerAction.Hibernate),
            3 => _allRecords.Where(r => r.Action == PowerAction.Shutdown || r.Action == PowerAction.ForceShutdown),
            4 => _allRecords.Where(r => r.Action == PowerAction.Restart || r.Action == PowerAction.ForceRestart),
            5 => _allRecords.Where(r => r.Timestamp.Year == now.Year && r.Timestamp.Month == now.Month),
            6 => _allRecords.Where(r => r.Timestamp.Year == now.Year),
            _ => _allRecords
        };

        IEnumerable<SessionRecord> sorted = SortCombo.SelectedIndex switch
        {
            1 => filtered.OrderBy(r => r.Timestamp),
            2 => filtered.OrderByDescending(r => r.WakeUpTimestamp.HasValue ? (r.WakeUpTimestamp.Value - r.Timestamp).TotalSeconds : 0),
            3 => filtered.OrderByDescending(r => r.ScheduledDurationSeconds),
            _ => filtered.OrderByDescending(r => r.Timestamp)
        };

        var allFilteredList = sorted.ToList();
        int totalCount = allFilteredList.Count;
        var pagedList = allFilteredList.Take(_currentLimit).ToList();

        RecordsListBox.ItemsSource = pagedList;
        PagingCounterText.Text = totalCount <= _currentLimit
            ? $"Showing all {totalCount} records"
            : $"Showing {pagedList.Count} of {totalCount} records";

        LoadMoreButton.Visibility = totalCount > _currentLimit ? Visibility.Visible : Visibility.Collapsed;
        EmptyNoticePanel.Visibility = totalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecordsListBox.Visibility = totalCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentLimit = 50;
        ApplyFilterAndSort();
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentLimit = 50;
        ApplyFilterAndSort();
    }

    private void LoadMoreButton_Click(object sender, RoutedEventArgs e)
    {
        _currentLimit += 50;
        ApplyFilterAndSort();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Title = "Export Sleep History",
            Filter = "JSON files (*.json)|*.json",
            FileName = $"SmartPowerScheduler_History_{DateTime.Now:yyyyMMdd}.json"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                HistoryService.ExportHistory(sfd.FileName);
                ShowFeedback($"✓ Exported {_allRecords.Count} records successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Import Sleep History",
            Filter = "JSON files (*.json)|*.json"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                HistoryService.ImportHistory(ofd.FileName, out int added, out int skipped);
                RefreshData();
                ShowFeedback($"✓ Added {added} new records ({skipped} duplicates skipped).");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(HistoryService.GenerateShareText());
            ShowFeedback("✓ Summary copied to clipboard!");
        }
        catch
        {
            // Clipboard transient exception
        }
    }

    private void DeleteRecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string recordId)
        {
            if (HistoryService.DeleteRecord(recordId))
            {
                RefreshData();
                ShowFeedback("Record deleted.");
            }
        }
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to permanently clear all session history?",
            "Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            HistoryService.ClearHistory();
            RefreshData();
            ShowFeedback("History cleared.");
        }
    }

    private async void ShowFeedback(string message)
    {
        FeedbackNoticeText.Text = message;
        FeedbackNoticeText.Visibility = Visibility.Visible;
        await Task.Delay(3000);
        FeedbackNoticeText.Visibility = Visibility.Collapsed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Closed?.Invoke();
}

