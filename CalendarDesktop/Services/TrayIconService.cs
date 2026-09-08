using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace CalendarDesktop.Services;

/// <summary>
/// Keeps WGC running in the notification area when the main window is closed.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _exitRequested;
    private bool _shownMinimizeTip;

    public bool IsExitRequested => _exitRequested;

    public void Initialize()
    {
        if (_notifyIcon != null) return;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open WGC", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Text = "WGC",
            Visible = true,
            ContextMenuStrip = menu,
            Icon = LoadAppIcon()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        AppLog.Logger.Information("Tray icon initialized");
    }

    public void HideMainWindowToTray(Window window)
    {
        window.ShowInTaskbar = false;
        window.Hide();

        if (_notifyIcon == null || _shownMinimizeTip) return;

        _shownMinimizeTip = true;
        _notifyIcon.BalloonTipTitle = "WGC is still running";
        _notifyIcon.BalloonTipText = "The app stays in the notification area so event reminders keep working. Right-click the icon to Exit.";
        _notifyIcon.ShowBalloonTip(4000);
    }

    public void ShowMainWindow()
    {
        var app = WpfApplication.Current;
        if (app == null) return;

        Window? window = app.MainWindow;
        if (window == null)
        {
            foreach (Window w in app.Windows)
            {
                if (w is MainWindow)
                {
                    window = w;
                    break;
                }
            }
        }

        if (window == null) return;

        window.ShowInTaskbar = true;
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public void ExitApplication()
    {
        _exitRequested = true;
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
        }

        WpfApplication.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_notifyIcon == null) return;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                var extracted = Icon.ExtractAssociatedIcon(exe);
                if (extracted != null) return extracted;
            }
        }
        catch
        {
            // fall through
        }

        return SystemIcons.Application;
    }
}
