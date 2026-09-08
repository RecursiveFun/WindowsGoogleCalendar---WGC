using Microsoft.Web.WebView2.WinForms;

namespace CalendarApp.Desktop;

public class MainForm : Form
{
    private readonly string _appUrl;
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Top,
        Height = 28,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 0, 0),
        Text = "Starting CalendarApp…"
    };

    public MainForm(string appUrl)
    {
        _appUrl = appUrl;
        Text = "CalendarApp";
        Width = 1280;
        Height = 840;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);

        Controls.Add(_webView);
        Controls.Add(_status);

        Shown += async (_, _) => await InitializeWebViewAsync();
        FormClosed += (_, _) => _webView.Dispose();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Navigate(_appUrl);
            _status.Text = _appUrl;
        }
        catch (Exception ex)
        {
            _status.Text = "WebView2 failed to start";
            MessageBox.Show(
                this,
                "Microsoft Edge WebView2 Runtime is required.\n\n" +
                "Install it from https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                ex.Message,
                "CalendarApp",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
