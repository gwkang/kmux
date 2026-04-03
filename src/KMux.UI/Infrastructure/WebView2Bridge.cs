using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using KMux.Core.Models;
using KMux.Core.Themes;
using KMux.UI.Services;
using KMux.UI.ViewModels;

namespace KMux.UI.Infrastructure;

public class WebView2Bridge : IDisposable
{
    private readonly WebView2 _webView;
    private readonly TerminalViewModel _vm;
    private readonly Dispatcher _dispatcher;
    private bool _initialized;
    private readonly List<string> _pendingMessages = new();

    /// <summary>Called on the UI thread when the JS side intercepts a KMux key chord.</summary>
    public Action<Key, ModifierKeys>? KmuxKeyPressed { get; set; }

    /// <summary>Called on the UI thread when the user right-clicks inside the terminal pane.</summary>
    public Action? ShowPaneContextMenu { get; set; }


    public WebView2Bridge(WebView2 webView, TerminalViewModel vm)
    {
        _webView    = webView;
        _vm         = vm;
        _dispatcher = webView.Dispatcher;

        _vm.OutputAvailable += OnOutputAvailable;
        AppSettingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        var msg = BuildSettingsMessage(AppSettingsService.Current, AppSettingsService.CurrentTheme);
        PostWhenReady(msg);
    }

    private static string BuildSettingsMessage(AppSettings settings, ThemeColors theme)
    {
        var payload = new
        {
            type       = "settings",
            fontFamily = $"'{settings.TerminalFontFamily}', 'Cascadia Mono', Consolas, 'Courier New', monospace",
            fontSize   = settings.TerminalFontSize,
            theme      = new
            {
                background          = theme.TermBg,
                foreground          = theme.TermFg,
                cursor              = theme.TermCursor,
                cursorAccent        = theme.TermCursorAccent,
                black               = theme.TermBlack,
                red                 = theme.TermRed,
                green               = theme.TermGreen,
                yellow              = theme.TermYellow,
                blue                = theme.TermBlue,
                magenta             = theme.TermMagenta,
                cyan                = theme.TermCyan,
                white               = theme.TermWhite,
                brightBlack         = theme.TermBrightBlack,
                brightRed           = theme.TermBrightRed,
                brightGreen         = theme.TermBrightGreen,
                brightYellow        = theme.TermBrightYellow,
                brightBlue          = theme.TermBrightBlue,
                brightMagenta       = theme.TermBrightMagenta,
                brightCyan          = theme.TermBrightCyan,
                brightWhite         = theme.TermBrightWhite,
                selectionBackground = theme.TermSelection,
            },
        };
        return JsonSerializer.Serialize(payload);
    }

    private void PostWhenReady(string msg)
    {
        lock (_pendingMessages)
        {
            if (!_initialized)
            {
                _pendingMessages.Add(msg);
                return;
            }
        }
        _dispatcher.InvokeAsync(() =>
            _webView.CoreWebView2.PostWebMessageAsString(msg),
            DispatcherPriority.Background);
    }

    public async Task InitializeAsync(string assetsPath)
    {
        await _webView.EnsureCoreWebView2Async();

        // Map virtual host so xterm.js loads from local assets without CORS issues
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "kmux.assets", assetsPath,
            CoreWebView2HostResourceAccessKind.Allow);

        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _webView.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;

        // Add host object so JS can call C# directly
        _webView.CoreWebView2.AddHostObjectToScript("kmuxBridge",
            new KmuxHostObject(_vm));

        _webView.CoreWebView2.Navigate("https://kmux.assets/terminal.html");
        // _initialized is set when JS sends "ready" — not here
    }

    // C# → xterm.js: write VT data
    private void OnOutputAvailable(object? sender, string vtData)
    {
        // JsonSerializer.Serialize correctly escapes all control characters including
        // ESC (\x1b / \u001b) that appear in VT sequences. Manual escaping missed
        // these and caused JSON.parse() to throw in JS, silently dropping output.
        var msg = $"{{\"type\":\"write\",\"data\":{JsonSerializer.Serialize(vtData)}}}";
        PostWhenReady(msg);
    }

    // xterm.js → C#: input / resize / title / ready
    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "ready":
                    lock (_pendingMessages)
                    {
                        _initialized = true;
                        // Send current settings first so xterm.js uses correct font/theme from the start
                        var settingsMsg = BuildSettingsMessage(AppSettingsService.Current, AppSettingsService.CurrentTheme);
                        _webView.CoreWebView2.PostWebMessageAsString(settingsMsg);
                        foreach (var pending in _pendingMessages)
                            _webView.CoreWebView2.PostWebMessageAsString(pending);
                        _pendingMessages.Clear();
                    }
                    break;
                case "input":
                    var data = root.GetProperty("data").GetString() ?? "";
                    _vm.HandleInput(data);
                    break;
                case "resize":
                    var cols = root.GetProperty("cols").GetInt32();
                    var rows = root.GetProperty("rows").GetInt32();
                    _vm.HandleResize(cols, rows);
                    break;
                case "title":
                    var title = root.GetProperty("data").GetString() ?? "Shell";
                    _dispatcher.InvokeAsync(() => _vm.Title = title);
                    break;
                case "copy":
                    if (root.TryGetProperty("data", out var copyData))
                    {
                        var sel = copyData.GetString();
                        if (!string.IsNullOrEmpty(sel))
                            _dispatcher.InvokeAsync(() => System.Windows.Clipboard.SetText(sel));
                    }
                    break;
                case "paste":
                    _dispatcher.InvokeAsync(() => Paste());
                    break;
                case "kmux_prefix":
                    KmuxKeyPressed?.Invoke(Key.B, ModifierKeys.Control);
                    break;
                case "kmux_chord":
                    var code  = root.GetProperty("code").GetString() ?? "";
                    var ctrl  = root.TryGetProperty("ctrl",  out var ce) && ce.GetBoolean();
                    var shift = root.TryGetProperty("shift", out var se) && se.GetBoolean();
                    var alt   = root.TryGetProperty("alt",   out var ae) && ae.GetBoolean();
                    var mods  = (ctrl  ? ModifierKeys.Control : ModifierKeys.None)
                              | (shift ? ModifierKeys.Shift   : ModifierKeys.None)
                              | (alt   ? ModifierKeys.Alt     : ModifierKeys.None);
                    KmuxKeyPressed?.Invoke(MapJsCode(code), mods);
                    break;
            }
        }
        catch { /* ignore malformed messages */ }
    }

    public void Paste()
    {
        var text = System.Windows.Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;
        // Route through xterm.js term.paste() so bracket paste mode (ESC[?2004h)
        // is honoured. Sending raw text via HandleInput bypasses this, causing
        // shells/Claude Code to interpret \r as Enter and immediately submit input.
        _ = _webView.CoreWebView2.ExecuteScriptAsync(
            $"term.paste({JsonSerializer.Serialize(text)})");
    }

    public async Task<string> GetSelectionAsync()
    {
        var json = await _webView.CoreWebView2.ExecuteScriptAsync("term.getSelection()");
        return JsonSerializer.Deserialize<string>(json) ?? "";
    }

    public void Dispose()
    {
        _vm.OutputAvailable -= OnOutputAvailable;
        AppSettingsService.SettingsChanged -= OnSettingsChanged;
        if (_webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.WebMessageReceived  -= OnWebMessageReceived;
            _webView.CoreWebView2.ContextMenuRequested -= OnContextMenuRequested;
        }
    }

    private void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        if (ShowPaneContextMenu is null) return;
        e.Handled = true; // suppress browser default context menu
        ShowPaneContextMenu();   // ContextMenuRequested fires on the UI thread
    }

    private static Key MapJsCode(string code) => code switch
    {
        "Minus"      => Key.OemMinus,
        "Backslash"  => Key.OemBackslash,
        "Comma"      => Key.OemComma,
        "ArrowUp"    => Key.Up,
        "ArrowDown"  => Key.Down,
        "ArrowLeft"  => Key.Left,
        "ArrowRight" => Key.Right,
        "KeyA" => Key.A, "KeyB" => Key.B, "KeyC" => Key.C, "KeyD" => Key.D,
        "KeyE" => Key.E, "KeyF" => Key.F, "KeyG" => Key.G, "KeyH" => Key.H,
        "KeyI" => Key.I, "KeyJ" => Key.J, "KeyK" => Key.K, "KeyL" => Key.L,
        "KeyM" => Key.M, "KeyN" => Key.N, "KeyO" => Key.O, "KeyP" => Key.P,
        "KeyQ" => Key.Q, "KeyR" => Key.R, "KeyS" => Key.S, "KeyT" => Key.T,
        "KeyU" => Key.U, "KeyV" => Key.V, "KeyW" => Key.W, "KeyX" => Key.X,
        "KeyY" => Key.Y, "KeyZ" => Key.Z,
        "Digit0" => Key.D0, "Digit1" => Key.D1, "Digit2" => Key.D2,
        "Digit3" => Key.D3, "Digit4" => Key.D4, "Digit5" => Key.D5,
        "Digit6" => Key.D6, "Digit7" => Key.D7, "Digit8" => Key.D8,
        "Digit9" => Key.D9,
        "F1"  => Key.F1,  "F2"  => Key.F2,  "F3"  => Key.F3,  "F4"  => Key.F4,
        "F5"  => Key.F5,  "F6"  => Key.F6,  "F7"  => Key.F7,  "F8"  => Key.F8,
        "F9"  => Key.F9,  "F10" => Key.F10, "F11" => Key.F11, "F12" => Key.F12,
        _ => Key.None
    };
}

/// <summary>COM-visible host object exposed to JavaScript as window.chrome.webview.hostObjects.kmuxBridge</summary>
[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class KmuxHostObject
{
    private readonly TerminalViewModel _vm;
    public KmuxHostObject(TerminalViewModel vm) => _vm = vm;
    public void SendInput(string data)          => _vm.HandleInput(data);
    public void SendResize(int cols, int rows)  => _vm.HandleResize(cols, rows);
    public void SetTitle(string title)          => _vm.Title = title;
}
