using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KMux.UI.Infrastructure;
using KMux.UI.ViewModels;

namespace KMux.UI.Views;

public partial class TerminalPane : UserControl, IDisposable
{
    private WebView2Bridge? _bridge;
    private bool _disposed;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(PaneViewModel),
            typeof(TerminalPane), new PropertyMetadata(null, OnViewModelChanged));

    public PaneViewModel? ViewModel
    {
        get => (PaneViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty AssetsPathProperty =
        DependencyProperty.Register(nameof(AssetsPath), typeof(string),
            typeof(TerminalPane), new PropertyMetadata(null, OnAssetsPathChanged));

    public string? AssetsPath
    {
        get => (string?)GetValue(AssetsPathProperty);
        set => SetValue(AssetsPathProperty, value);
    }

    private static void OnAssetsPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TerminalPane pane && pane.IsLoaded)
            pane.TryInitialize();
    }

    public TerminalPane()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TerminalPane pane)
        {
            if (e.OldValue is PaneViewModel oldVm)
                oldVm.PropertyChanged -= pane.OnVmPropertyChanged;
            if (e.NewValue is PaneViewModel newVm)
            {
                newVm.PropertyChanged += pane.OnVmPropertyChanged;
                pane._lastIsHighlighted = null; // force full visual init on next UpdateFocusBorder
                pane.Dispatcher.InvokeAsync(pane.UpdateHeader);
                pane.Dispatcher.InvokeAsync(pane.UpdateFocusBorder);
            }
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PaneViewModel.IsFocused))
        {
            Dispatcher.InvokeAsync(() =>
            {
                UpdateFocusBorder();
                if (ViewModel?.IsFocused == true)
                {
                    // Move Win32 keyboard focus to the WebView2 HWND so that
                    // xterm.js's attachCustomKeyEventHandler receives keystrokes.
                    // Without this, programmatic focus (e.g. after tab switch via
                    // keyboard shortcut) leaves neither WPF nor WebView2 with focus,
                    // causing the next shortcut press to be silently dropped.
                    if (!WebView.IsKeyboardFocusWithin)
                        WebView.Focus();
                    _ = WebView.ExecuteScriptAsync("window.termFocus && window.termFocus()");
                }
            });
        }
        else if (e.PropertyName == nameof(PaneViewModel.IsActive)
              || e.PropertyName == nameof(PaneViewModel.IsClaudeBusy)
              || e.PropertyName == nameof(PaneViewModel.IsClaudeReady))
        {
            Dispatcher.InvokeAsync(() =>
            {
                UpdateFocusBorder();
                UpdateActivityBar();
            });
        }
        else if (e.PropertyName == nameof(PaneViewModel.DisplayPath)
              || e.PropertyName == nameof(PaneViewModel.ClaudeActivity)
              || e.PropertyName == nameof(PaneViewModel.PaneTitle))
        {
            Dispatcher.InvokeAsync(UpdateHeader);
        }
    }

    private void UpdateHeader()
    {
        PathText.Text     = !string.IsNullOrEmpty(ViewModel?.PaneTitle)
                            ? ViewModel.PaneTitle
                            : ViewModel?.DisplayPath ?? "";
        ActivityText.Text = ViewModel?.ClaudeActivity ?? "";
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) BeginPaneRename();
    }

    private void BeginPaneRename()
    {
        if (ViewModel is null) return;
        PaneTitleEdit.Text = string.IsNullOrEmpty(ViewModel.PaneTitle)
            ? ViewModel.DisplayPath
            : ViewModel.PaneTitle;
        HeaderDisplay.Visibility = Visibility.Collapsed;
        PaneTitleEdit.Visibility = Visibility.Visible;
        PaneTitleEdit.SelectAll();
        PaneTitleEdit.Focus();
    }

    private void CommitPaneRename()
    {
        if (ViewModel is null) return;
        PaneTitleEdit.Visibility = Visibility.Collapsed;
        HeaderDisplay.Visibility = Visibility.Visible;
        ViewModel.PaneTitle = PaneTitleEdit.Text.Trim();
        UpdateHeader();
    }

    private void PaneTitleEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  { CommitPaneRename(); e.Handled = true; }
        if (e.Key == Key.Escape) { CancelPaneRename(); e.Handled = true; }
    }

    private void PaneTitleEdit_LostFocus(object sender, RoutedEventArgs e) => CommitPaneRename();

    private void CancelPaneRename()
    {
        PaneTitleEdit.Visibility = Visibility.Collapsed;
        HeaderDisplay.Visibility = Visibility.Visible;
    }

    private static readonly SolidColorBrush ActivityBarActiveBrush;
    private static readonly SolidColorBrush ActivityBarClaudeBrush;
    private static readonly SolidColorBrush ActivityBarReadyBrush;
    private static readonly DoubleAnimation PulseAnimation;
    private static readonly DoubleAnimation FadeOutAnimation;
    private static readonly DoubleAnimation FadeInAnimation;
    private static readonly DoubleAnimation OverlayFadeInAnimation;
    private static readonly DoubleAnimation OverlayFadeOutAnimation;  // 150 ms — snappier than the activity bar (200–300 ms) for a focus transition

    private bool? _lastIsHighlighted;

    static TerminalPane()
    {
        ActivityBarActiveBrush = new SolidColorBrush(Color.FromRgb(166, 227, 161)); // green
        ActivityBarActiveBrush.Freeze();
        ActivityBarClaudeBrush = new SolidColorBrush(Color.FromRgb(249, 226, 175)); // yellow
        ActivityBarClaudeBrush.Freeze();
        ActivityBarReadyBrush  = new SolidColorBrush(Color.FromRgb(137, 220, 235)); // sky
        ActivityBarReadyBrush.Freeze();

        var ease = new SineEase();
        ease.Freeze();
        PulseAnimation = new DoubleAnimation(0.5, 1.0, new Duration(TimeSpan.FromMilliseconds(700)))
        {
            AutoReverse    = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        PulseAnimation.Freeze();
        FadeOutAnimation = new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(300)));
        FadeOutAnimation.Freeze();
        FadeInAnimation = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(200)));
        FadeInAnimation.Freeze();
        OverlayFadeInAnimation  = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(150)));
        OverlayFadeInAnimation.Freeze();
        OverlayFadeOutAnimation = new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(150)));
        OverlayFadeOutAnimation.Freeze();
    }

    private void UpdateFocusBorder()
    {
        // isHighlighted: keyboard focus only — Claude-ready state is communicated separately via FocusBorder green color
        var isHighlighted = ViewModel?.IsFocused == true;

        if (ViewModel?.IsClaudeReady == true)
        {
            // Green border: Claude finished and waiting for input
            FocusBorder.BorderBrush = ThemeHelper.GetBrush(ThemeResourceKeys.Green, Color.FromRgb(166, 227, 161));
        }
        else if (ViewModel?.IsFocused == true)
        {
            FocusBorder.BorderBrush = ThemeHelper.GetBrush(ThemeResourceKeys.Accent, Color.FromRgb(137, 180, 250));
        }
        else
        {
            // Transparent — let the accent border stand out by contrast, not compete with a dim fallback
            FocusBorder.BorderBrush = Brushes.Transparent;
        }

        // Guard: only update header/overlay when highlighted state actually changes to avoid
        // restarting BeginAnimation mid-flight (which causes a visible flicker on rapid state transitions)
        if (_lastIsHighlighted != isHighlighted)
        {
            _lastIsHighlighted = isHighlighted;
            UpdateHeaderStyle(isHighlighted);
            UpdateInactiveOverlay(isHighlighted);
        }
    }

    private void UpdateHeaderStyle(bool isHighlighted)
    {
        if (isHighlighted)
        {
            HeaderBorder.Background = ThemeHelper.GetBrush(ThemeResourceKeys.Surface0, Color.FromRgb(49, 50, 68));
            PathText.Foreground     = ThemeHelper.GetBrush(ThemeResourceKeys.Subtext1, Color.FromRgb(166, 173, 200));
        }
        else
        {
            HeaderBorder.Background = ThemeHelper.GetBrush(ThemeResourceKeys.Crust, Color.FromRgb(17, 17, 27));
            PathText.Foreground     = ThemeHelper.GetBrush(ThemeResourceKeys.Subtext0, Color.FromRgb(88, 91, 112));
        }
    }

    private void UpdateInactiveOverlay(bool isHighlighted)
    {
        InactiveOverlay.BeginAnimation(OpacityProperty,
            isHighlighted ? OverlayFadeOutAnimation : OverlayFadeInAnimation);
    }

    private void UpdateActivityBar()
    {
        var isClaudeBusy  = ViewModel?.IsClaudeBusy  == true;
        var isClaudeReady = ViewModel?.IsClaudeReady == true;
        var isActive      = ViewModel?.IsActive      == true;

        if (isClaudeBusy)
        {
            ActivityBar.Fill = ActivityBarClaudeBrush;
            ActivityBar.BeginAnimation(OpacityProperty, PulseAnimation);
        }
        else if (isClaudeReady)
        {
            // Solid sky-blue bar: Claude is idle and waiting for next command
            ActivityBar.Fill = ActivityBarReadyBrush;
            ActivityBar.BeginAnimation(OpacityProperty, FadeInAnimation);
        }
        else if (isActive)
        {
            ActivityBar.Fill = ActivityBarActiveBrush;
            ActivityBar.BeginAnimation(OpacityProperty, PulseAnimation);
        }
        else
        {
            ActivityBar.BeginAnimation(OpacityProperty, FadeOutAnimation);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => TryInitialize();

    private async void TryInitialize()
    {
        if (ViewModel is null || AssetsPath is null || _bridge is not null) return;

        try
        {
            _bridge = new WebView2Bridge(WebView, ViewModel.Terminal);
            _bridge.KmuxKeyPressed = (key, mods) =>
            {
                if (Window.GetWindow(this) is TerminalWindow win)
                    win.HandleAcceleratorKey(key, mods);
            };
            _bridge.ShowPaneContextMenu = () =>
            {
                if (Window.GetWindow(this) is TerminalWindow win)
                    win.ShowPaneContextMenu(this, _bridge.GetSelectionAsync, _bridge.Paste);
            };

            await _bridge.InitializeAsync(AssetsPath);

            // If the pane was removed from the visual tree while WebView2 was initializing
            // (e.g. user switched to dashboard), abort cleanly so the next Loaded event can retry.
            if (!IsLoaded)
            {
                _bridge.Dispose();
                _bridge = null;
            }
        }
        catch
        {
            // Visual tree was removed mid-init (e.g. user switched to dashboard before
            // WebView2 finished). Reset so Loaded can retry when the pane re-enters the tree.
            _bridge?.Dispose();
            _bridge = null;
        }
    }

    public void Refit() => _bridge?.Refit();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bridge?.Dispose();
        if (ViewModel is not null)
            ViewModel.PropertyChanged -= OnVmPropertyChanged;
    }
}
