using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SmartSleepScheduler.Core;
using SmartSleepScheduler.Core.Models;
using SmartSleepScheduler.Core.Services;

namespace SmartSleepScheduler.UI.Views;

public partial class HistoryView : UserControl
{
    public event Action? Closed;

    private List<SessionRecord> _allRecords = new();

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
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (FilterCombo == null || RecordsListBox == null) return;

        var now = DateTime.Now;
        IEnumerable<SessionRecord> filtered = FilterCombo.SelectedIndex switch
        {
            1 => _allRecords.Where(r => r.Timestamp.Year == now.Year && r.Timestamp.Month == now.Month),
            2 => _allRecords.Where(r => r.Timestamp.Year == now.Year),
            3 => _allRecords.Where(r => r.Action == PowerAction.Sleep),
            4 => _allRecords.Where(r => r.Action == PowerAction.Shutdown || r.Action == PowerAction.ForceShutdown),
            _ => _allRecords
        };

        var list = filtered.ToList();
        RecordsListBox.ItemsSource = list;
        EmptyNoticePanel.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecordsListBox.Visibility = list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Title = "Export Sleep History",
            Filter = "JSON files (*.json)|*.json",
            FileName = $"SmartSleepScheduler_History_{DateTime.Now:yyyyMMdd}.json"
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

