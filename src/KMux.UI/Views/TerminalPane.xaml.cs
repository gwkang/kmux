using System.IO;
using System.Windows;
using System.Windows.Controls;
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
                pane.Dispatcher.InvokeAsync(pane.UpdateHeader);
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
                    _ = WebView.ExecuteScriptAsync("window.termFocus && window.termFocus()");
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
              || e.PropertyName == nameof(PaneViewModel.ClaudeActivity))
        {
            Dispatcher.InvokeAsync(UpdateHeader);
        }
    }

    private void UpdateHeader()
    {
        PathText.Text     = ViewModel?.DisplayPath    ?? "";
        ActivityText.Text = ViewModel?.ClaudeActivity ?? "";
    }

    private static readonly SolidColorBrush ActivityBarActiveBrush;
    private static readonly SolidColorBrush ActivityBarClaudeBrush;
    private static readonly SolidColorBrush ActivityBarReadyBrush;
    private static readonly DoubleAnimation PulseAnimation;
    private static readonly DoubleAnimation FadeOutAnimation;
    private static readonly DoubleAnimation FadeInAnimation;

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
    }

    private void UpdateFocusBorder()
    {
        if (ViewModel?.IsClaudeReady == true)
        {
            // Bright green border: Claude finished and waiting for input
            FocusBorder.BorderBrush     = ThemeHelper.GetBrush(ThemeResourceKeys.Green, Color.FromRgb(166, 227, 161));
            FocusBorder.BorderThickness = new Thickness(4);
        }
        else
        {
            FocusBorder.BorderBrush = ViewModel?.IsFocused == true
                ? ThemeHelper.GetBrush(ThemeResourceKeys.Accent,   Color.FromRgb(137, 180, 250))
                : ThemeHelper.GetBrush(ThemeResourceKeys.Surface0, Color.FromRgb(49,  50,  68));
            FocusBorder.BorderThickness = new Thickness(ViewModel?.IsClaudeBusy == true ? 3 : 2);
        }
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

        _bridge = new WebView2Bridge(WebView, ViewModel.Terminal);
        _bridge.KmuxKeyPressed = (key, mods) =>
        {
            if (Window.GetWindow(this) is TerminalWindow win)
                win.HandleAcceleratorKey(key, mods);
        };
        _bridge.ShowPaneContextMenu = () =>
        {
            if (Window.GetWindow(this) is TerminalWindow win)
                win.ShowPaneContextMenu(this);
        };
        await _bridge.InitializeAsync(AssetsPath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bridge?.Dispose();
        if (ViewModel is not null)
            ViewModel.PropertyChanged -= OnVmPropertyChanged;
    }
}
