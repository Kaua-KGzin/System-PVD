using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Archlab.Desktop;

/// <summary>The cashier window: one WebView2 filling the frame, pointed at the in-process API.</summary>
internal sealed class MainForm : Form
{
    private readonly WebView2 _webView;
    private readonly string _startUrl;

    private readonly UiPreferences _preferences = UiPreferences.Load();

    private bool _isFullScreen;
    private FormWindowState _restoreState;
    private FormBorderStyle _restoreBorder;
    private Rectangle _restoreBounds;

    public MainForm(string startUrl)
    {
        _startUrl = startUrl;

        Text = "ARCHNEXUS";
        Icon = AppIcon.Load();
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1024, 700);
        StartPosition = FormStartPosition.CenterScreen;

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        Load += OnLoadAsync;
    }

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        try
        {
            // Keep the browser profile with the rest of the app's state instead of beside the
            // executable, which may sit in a read-only location.
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: Path.Combine(Program.DataDirectory, "webview"));

            await _webView.EnsureCoreWebView2Async(environment);

            var settings = _webView.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.IsSwipeNavigationEnabled = false;

            // A point of sale has no address bar: a popup would be a window the cashier cannot
            // close and cannot navigate. Send every one back into the main frame.
            _webView.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                _webView.CoreWebView2.Navigate(args.Uri);
            };

            // The WebView holds keyboard focus for the whole session, so the form's KeyPreview
            // never sees F11, and the WinForms wrapper does not expose the controller whose
            // AcceleratorKeyPressed event would. Listening in the page and posting to the host
            // is the path that does not depend on either.
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                """
                // This script runs for every document the WebView creates, so without the guard
                // a second document would register a second listener and each F11 would toggle
                // twice — landing back where it started.
                if (!window.__archnexusKeyHandler) {
                  window.__archnexusKeyHandler = true;
                  window.addEventListener('keydown', function (event) {
                    if (event.repeat) { return; }
                    if (event.key === 'F11') {
                      event.preventDefault();
                      window.chrome.webview.postMessage('toggle-fullscreen');
                    } else if (event.key === 'Escape') {
                      window.chrome.webview.postMessage('exit-fullscreen');
                    }
                  });
                }
                """);

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Web content can request fullscreen on its own (a chart, a receipt preview). Follow
            // it, or the element would expand inside a window that stays framed.
            _webView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
                SetFullScreen(_webView.CoreWebView2.ContainsFullScreenElement);

            _webView.CoreWebView2.Navigate(_startUrl);

            if (_preferences.FullScreen)
            {
                SetFullScreen(true);
            }
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                "O WebView2 Runtime nao esta instalado nesta maquina.\n\n" +
                "Instale pelo link abaixo e abra o ARCHNEXUS novamente:\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/",
                "ARCHNEXUS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            Close();
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string message;
        try
        {
            message = e.TryGetWebMessageAsString();
        }
        catch (ArgumentException)
        {
            // Any non-string message came from page code that is not ours to interpret.
            return;
        }

        switch (message)
        {
            case "toggle-fullscreen":
                SetFullScreen(!_isFullScreen);
                RememberWindowMode();
                break;

            // Esc is the reflex when a screen has no visible way out. It only leaves fullscreen,
            // so it can never close the till by accident.
            case "exit-fullscreen" when _isFullScreen:
                SetFullScreen(false);
                RememberWindowMode();
                break;
        }
    }

    /// <summary>
    /// Only a deliberate keypress is remembered. Fullscreen entered by web content is transient
    /// and must not decide how the till opens tomorrow.
    /// </summary>
    private void RememberWindowMode()
    {
        _preferences.FullScreen = _isFullScreen;
        _preferences.Save();
    }

    private void SetFullScreen(bool enabled)
    {
        if (enabled == _isFullScreen)
        {
            return;
        }

        if (enabled)
        {
            _restoreState = WindowState;
            _restoreBorder = FormBorderStyle;
            _restoreBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

            // A maximized window with its border removed still stops at the taskbar. Dropping to
            // Normal first, then taking the screen's own bounds, is what actually covers it.
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize = Size.Empty;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            FormBorderStyle = _restoreBorder;
            MinimumSize = new Size(1024, 700);
            Bounds = _restoreBounds;
            WindowState = _restoreState;
        }

        _isFullScreen = enabled;
    }
}
