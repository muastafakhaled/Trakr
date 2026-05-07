using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Windows;
using JiraTimeTracker.Models;
using JiraTimeTracker.Services;
using JiraTimeTracker.Windows;

namespace JiraTimeTracker.Tray;

public class TrayManager : IDisposable
{
    private readonly TrackerService _tracker;
    private readonly ConfigService  _config;
    private readonly HomeWindow     _home;
    private NotifyIcon?  _tray;
    private System.Windows.Forms.Timer? _clockTimer;

    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _stopItem;

    public TrayManager(TrackerService tracker, ConfigService config, HomeWindow home)
    {
        _tracker = tracker;
        _config  = config;
        _home    = home;
        _tracker.StateChanged += OnStateChanged;
        _tracker.Notified     += (title, msg) => ShowBalloon(title, msg);
    }

    public void Initialize()
    {
        _tray = new NotifyIcon
        {
            Visible = true,
            Icon    = MakeIcon(false),
            Text    = "Trakr"
        };

        var menu = new ContextMenuStrip();
        menu.BackColor  = Color.FromArgb(30, 30, 46);
        menu.ForeColor  = Color.FromArgb(230, 237, 243);
        menu.Font       = new Font("Segoe UI", 9f);
        menu.Renderer   = new DarkMenuRenderer();

        _statusItem = new ToolStripMenuItem("Idle — no Jira branch") { Enabled = false };
        _statusItem.Font = new Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);

        _stopItem = new ToolStripMenuItem("⏹  Stop && Log", null, OnStopClick) { Enabled = false };

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("▶  Start Timer Manually", null, OnManualStartClick));
        menu.Items.Add(_stopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("📋  Sprint Report",  null, OnReportClick));
        menu.Items.Add(new ToolStripMenuItem("📁  Manage Repos",   null, OnManageReposClick));
        menu.Items.Add(new ToolStripMenuItem("⚙   Settings",       null, OnSettingsClick));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("✕  Exit",            null, OnExitClick));

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenHome();

        _clockTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        _clockTimer.Tick += (_, _) => UpdateTooltip();
        _clockTimer.Start();

        if (!_config.IsConfigured())
            ShowBalloon("Setup required", "Click Settings to configure your Jira connection.", ToolTipIcon.Warning);
    }

    private void OnStateChanged()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            UpdateTooltip();
            if (_statusItem == null || _stopItem == null) return;

            if (_tracker.State.IsTracking)
            {
                var issue = _tracker.State.ActiveIssue;
                var dur   = Session.FormatDuration(_tracker.GetWorkedSeconds());
                var mode  = _tracker.State.IsManual ? " (manual)" : "";
                var paused = _tracker.State.IsPaused ? " ⏸" : "";
                _statusItem.Text  = $"● {issue}  {dur}{mode}{paused}";
                _stopItem.Enabled = true;
                _tray!.Icon = MakeIcon(true, _tracker.State.IsPaused);
            }
            else
            {
                _statusItem.Text  = "Idle — no Jira branch";
                _stopItem.Enabled = false;
                _tray!.Icon = MakeIcon(false);
            }
        });
    }

    private void UpdateTooltip()
    {
        if (_tray == null) return;
        if (_tracker.State.IsTracking)
        {
            var issue  = _tracker.State.ActiveIssue;
            var repo   = System.IO.Path.GetFileName(_tracker.State.ActiveRepo);
            var dur    = Session.FormatDuration(_tracker.GetWorkedSeconds());
            var paused = _tracker.State.IsPaused ? " (paused)" : "";
            _tray.Text = $"{issue} — {dur}{paused}\n{repo}".Length > 63
                ? $"{issue} — {dur}{paused}"
                : $"{issue} — {dur}{paused}\n{repo}";
        }
        else
        {
            _tray.Text = "Trakr — Idle";
        }
    }

    public void ShowBalloon(string title, string msg, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _tray?.ShowBalloonTip(4000, title, msg, icon);
    }

    private void OpenHome()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _home.Show();
            _home.Activate();
        });
    }

    private void OnManualStartClick(object? s, EventArgs e) => OpenHome();
    private void OnStopClick(object? s, EventArgs e)        => _tracker.ManualStop();

    private void OnReportClick(object? s, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new SprintReportWindow(new DatabaseService(_config));
            win.Show();
        });
    }

    private void OnManageReposClick(object? s, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new ManageReposWindow(_config);
            win.Show();
        });
    }

    private void OnSettingsClick(object? s, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow(_config, new JiraService(_config));
            win.Show();
        });
    }

    private void OnExitClick(object? s, EventArgs e)
    {
        _tracker.Stop();
        System.Windows.Application.Current.Shutdown();
    }

    private static Icon MakeIcon(bool active, bool paused = false)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        var color = paused  ? Color.FromArgb(210, 153, 34) :
                    active  ? Color.FromArgb(63, 185, 80)  :
                              Color.FromArgb(125, 133, 144);
        g.FillEllipse(new SolidBrush(color), 1, 1, 14, 14);
        var h = bmp.GetHicon();
        return Icon.FromHandle(h);
    }

    public void Dispose()
    {
        _clockTimer?.Stop();
        _tray?.Dispose();
    }
}

// Dark renderer for context menu
public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Color.FromArgb(230, 237, 243) : Color.FromArgb(125, 133, 144);
        base.OnRenderItemText(e);
    }
}

public class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected         => Color.FromArgb(45, 45, 80);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(45, 45, 80);
    public override Color MenuItemSelectedGradientEnd   => Color.FromArgb(45, 45, 80);
    public override Color MenuItemPressedGradientBegin  => Color.FromArgb(55, 55, 90);
    public override Color MenuItemPressedGradientEnd    => Color.FromArgb(55, 55, 90);
    public override Color ToolStripDropDownBackground   => Color.FromArgb(30, 30, 46);
    public override Color ImageMarginGradientBegin      => Color.FromArgb(30, 30, 46);
    public override Color ImageMarginGradientMiddle     => Color.FromArgb(30, 30, 46);
    public override Color ImageMarginGradientEnd        => Color.FromArgb(30, 30, 46);
    public override Color MenuBorder                    => Color.FromArgb(58, 58, 90);
    public override Color SeparatorDark                 => Color.FromArgb(58, 58, 90);
    public override Color SeparatorLight                => Color.FromArgb(58, 58, 90);
}