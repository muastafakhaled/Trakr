using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using JiraTimeTracker.Services;
using Microsoft.Win32;

namespace JiraTimeTracker.Windows;

// ── Lightweight ViewModel — exposes Brush directly so XAML never needs converters
internal class LogEntryVM
{
    private static readonly SolidColorBrush BDebug   = Freeze(0x48,0x4f,0x58);
    private static readonly SolidColorBrush BInfo    = Freeze(0x8b,0x94,0x9e);
    private static readonly SolidColorBrush BWarning = Freeze(0xd2,0x99,0x22);
    private static readonly SolidColorBrush BError   = Freeze(0xf8,0x51,0x49);

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
        b2.Freeze();
        return b2;
    }

    public string TimeStr    { get; }
    public string LevelTag   { get; }
    public string Category   { get; }
    public string Message    { get; }
    public Brush  LevelBrush { get; }

    public LogEntryVM(LogEntry e)
    {
        TimeStr    = e.Time.ToString("HH:mm:ss.fff");
        LevelTag   = e.LevelTag;
        Category   = e.Category;
        Message    = e.Message;
        LevelBrush = e.Level switch
        {
            LogLevel.Info    => BInfo,
            LogLevel.Warning => BWarning,
            LogLevel.Error   => BError,
            _                => BDebug
        };
    }
}

// ── Window ────────────────────────────────────────────────────────────────────
public partial class LogViewerWindow : Window
{
    private readonly LogService      _log;
    private readonly DispatcherTimer _timer;
    private bool _suppressRefresh;

    public LogViewerWindow(LogService log)
    {
        _log = log;
        InitializeComponent();

        // Wire filter events here — avoids any XAML timing issues
        SearchBox    .TextChanged      += (_, _) => Refresh();
        LevelCombo   .SelectionChanged += (_, _) => { if (!_suppressRefresh) Refresh(); };
        CategoryCombo.SelectionChanged += (_, _) => { if (!_suppressRefresh) Refresh(); };

        LevelCombo.SelectedIndex = 0;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        _log.EntryAdded += OnEntryAdded;

        RebuildCategoryCombo();
        Refresh();
    }

    private void OnEntryAdded(LogEntry _)
        => Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Background);

    // ── Rebuild category dropdown ──────────────────────────────────
    private void RebuildCategoryCombo()
    {
        _suppressRefresh = true;

        var current = (CategoryCombo.SelectedItem as ComboBoxItem)?.Content as string;
        CategoryCombo.Items.Clear();
        CategoryCombo.Items.Add(new ComboBoxItem { Content = "All categories" });
        foreach (var cat in _log.GetCategories())
            CategoryCombo.Items.Add(new ComboBoxItem { Content = cat });

        // Restore previous selection or default to first
        var restored = false;
        if (current != null)
            foreach (ComboBoxItem item in CategoryCombo.Items)
                if (item.Content as string == current)
                { CategoryCombo.SelectedItem = item; restored = true; break; }
        if (!restored) CategoryCombo.SelectedIndex = 0;

        _suppressRefresh = false;
    }

    // ── Refresh list ───────────────────────────────────────────────
    private void Refresh()
    {
        var minLevel = LevelCombo.SelectedIndex switch
        {
            1 => LogLevel.Info,
            2 => LogLevel.Warning,
            3 => LogLevel.Error,
            _ => LogLevel.Debug
        };

        var catItem  = CategoryCombo.SelectedItem as ComboBoxItem;
        var category = catItem?.Content as string;
        if (category is null or "All categories") category = null;

        var search = SearchBox.Text.Trim();

        var filtered = _log.GetFiltered(minLevel, category,
                           string.IsNullOrEmpty(search) ? null : search);
        var vms = filtered.Select(e => new LogEntryVM(e)).ToList();

        LogList.ItemsSource = vms;

        CountLabel.Text = $"{vms.Count} entries";
        FileLabel.Text  = _log.WriteToFile && !string.IsNullOrEmpty(_log.LogFilePath)
            ? $"📄  {_log.LogFilePath}"
            : "File logging off";

        RebuildCategoryCombo();

        if (AutoScrollCheck.IsChecked == true && vms.Count > 0)
            Scroller.ScrollToBottom();
    }

    // ── Toolbar buttons ────────────────────────────────────────────
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _log.Clear();
        Refresh();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title    = "Export Logs",
            Filter   = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
            FileName = $"JiraTracker_{DateTime.Now:yyyyMMdd_HHmm}.log"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _log.ExportToFile(dlg.FileName);
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FileLabel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!string.IsNullOrEmpty(_log.LogFilePath))
            try { Process.Start("explorer.exe", $"/select,\"{_log.LogFilePath}\""); } catch { }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _log.EntryAdded -= OnEntryAdded;
    }
}
