using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KMux.Layout;
using KMux.UI.Infrastructure;
using KMux.UI.ViewModels;

namespace KMux.UI.Views;

public partial class PaneContainer : UserControl
{
    public static readonly DependencyProperty TabProperty =
        DependencyProperty.Register(nameof(Tab), typeof(TabViewModel),
            typeof(PaneContainer), new PropertyMetadata(null, OnTabChanged));

    public TabViewModel? Tab
    {
        get => (TabViewModel?)GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    public static readonly DependencyProperty AssetsPathProperty =
        DependencyProperty.Register(nameof(AssetsPath), typeof(string),
            typeof(PaneContainer), new PropertyMetadata(null, OnAssetsPathChanged));

    private static void OnAssetsPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var container = (PaneContainer)d;
        if (e.NewValue is string newPath)
        {
            foreach (var cache in container._tabPaneCache.Values)
                foreach (var pane in cache.Values)
                    pane.AssetsPath = newPath;
        }
    }

    public string? AssetsPath
    {
        get => (string?)GetValue(AssetsPathProperty);
        set => SetValue(AssetsPathProperty, value);
    }

    // Per-tab cache of TerminalPane instances keyed by (TabId → PaneId → TerminalPane).
    // Tabs keep their panes alive across tab switches; panes are only disposed when
    // the tab itself is closed (TabViewModel.Disposing fires) or the window closes.
    private readonly Dictionary<Guid, Dictionary<Guid, TerminalPane>> _tabPaneCache = new();

    private Dictionary<Guid, TerminalPane> GetOrCreateTabCache(TabViewModel tab)
    {
        if (!_tabPaneCache.TryGetValue(tab.Id, out var cache))
        {
            cache = new Dictionary<Guid, TerminalPane>();
            _tabPaneCache[tab.Id] = cache;
        }
        return cache;
    }

    public PaneContainer()
    {
        InitializeComponent();
    }

    private static void OnTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var container = (PaneContainer)d;

        if (e.OldValue is TabViewModel oldTab)
        {
            oldTab.PropertyChanged -= container.OnTabPropertyChanged;
            oldTab.Disposing       -= container.OnTabDisposing;
        }

        if (e.NewValue is TabViewModel newTab)
        {
            newTab.PropertyChanged += container.OnTabPropertyChanged;
            newTab.Disposing       += container.OnTabDisposing;
            container.Rebuild(newTab.LayoutRoot);
        }
        else
        {
            // Tab set to null (window closing) — dispose all cached panes
            container.EvictAllCaches();
        }
    }

    private void OnTabDisposing(object? sender, EventArgs e)
    {
        if (sender is TabViewModel tab)
            EvictCacheFor(tab);
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.LayoutRoot) && Tab is not null)
            Dispatcher.InvokeAsync(() => Rebuild(Tab.LayoutRoot));
    }

    private void Rebuild(LayoutNode root)
    {
        RootGrid.Children.Clear();
        RootGrid.RowDefinitions.Clear();
        RootGrid.ColumnDefinitions.Clear();

        // Dispose any panes no longer in this tab's layout
        if (Tab is not null && _tabPaneCache.TryGetValue(Tab.Id, out var cache))
        {
            var activePaneIds = new HashSet<Guid>(GetAllLeafIds(root));
            foreach (var id in cache.Keys.Except(activePaneIds).ToList())
            {
                cache[id].Dispose();
                cache.Remove(id);
            }
        }

        RootGrid.Children.Add(BuildElement(root));

        // After the WPF layout pass completes and WebView2 HWNDs are at their final
        // sizes, re-fit all terminals. ResizeObserver inside xterm.js should handle
        // most cases, but can be missed when panes are reparented during a rebuild.
        Dispatcher.InvokeAsync(() =>
        {
            if (Tab is not null && _tabPaneCache.TryGetValue(Tab.Id, out var paneCache))
                foreach (var pane in paneCache.Values)
                    pane.Refit();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void EvictCacheFor(TabViewModel tab)
    {
        if (_tabPaneCache.TryGetValue(tab.Id, out var cache))
        {
            foreach (var pane in cache.Values) pane.Dispose();
            _tabPaneCache.Remove(tab.Id);
        }
    }

    private void EvictAllCaches()
    {
        foreach (var cache in _tabPaneCache.Values)
            foreach (var pane in cache.Values)
                pane.Dispose();
        _tabPaneCache.Clear();
    }

    private UIElement BuildElement(LayoutNode node) => node switch
    {
        LeafNode  leaf  => BuildLeaf(leaf),
        SplitNode split => BuildSplit(split),
        _               => new Grid()
    };

    private UIElement BuildLeaf(LeafNode leaf)
    {
        var vm = Tab?.GetPane(leaf.PaneId);
        if (vm is null) return new Grid { Background = System.Windows.Media.Brushes.Black };

        var paneCache = Tab is not null ? GetOrCreateTabCache(Tab) : new Dictionary<Guid, TerminalPane>();

        // Reuse cached instance; only create a new TerminalPane when we see a new PaneId
        if (!paneCache.TryGetValue(leaf.PaneId, out var termPane))
        {
            termPane = new TerminalPane { ViewModel = vm, AssetsPath = AssetsPath };
            // GotFocus fires reliably when the WebView2 HwndHost gains Win32 focus;
            // MouseDown does not — WebView2 captures clicks before WPF sees them.
            termPane.GotFocus += (_, _) => Tab?.FocusPane(leaf.PaneId);
            paneCache[leaf.PaneId] = termPane;
        }
        else if (termPane.Parent is Panel oldParent)
        {
            // Detach from old parent before re-adding to the new layout grid.
            // Rebuild() only clears RootGrid's direct child, so deeply nested
            // TerminalPane elements still hold a reference to the previous inner
            // Grid.  WPF throws if an element already has a visual parent.
            oldParent.Children.Remove(termPane);
        }

        return termPane;
    }

    private UIElement BuildSplit(SplitNode split)
    {
        var grid     = new Grid();
        var first    = BuildElement(split.First);
        var second   = BuildElement(split.Second);
        bool isHoriz = split.Direction == SplitDirection.Horizontal;

        var splitter = CreateSplitter(isHoriz ? Orientation.Horizontal : Orientation.Vertical);

        if (isHoriz)
        {
            // Horizontal split = top / bottom — horizontal divider bar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(split.Ratio, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - split.Ratio, GridUnitType.Star) });
            Grid.SetRow(first,    0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(second,   2);
        }
        else
        {
            // Vertical split = left / right — vertical divider bar
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(split.Ratio, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - split.Ratio, GridUnitType.Star) });
            Grid.SetColumn(first,    0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(second,   2);
        }

        grid.Children.Add(first);
        grid.Children.Add(splitter);
        grid.Children.Add(second);
        return grid;
    }

    private static GridSplitter CreateSplitter(Orientation orientation)
    {
        // Use shared resource brushes directly so theme changes propagate automatically
        var normalBrush = ThemeHelper.GetBrush(ThemeResourceKeys.Surface0, Color.FromRgb(49, 50, 68));
        var hoverBrush  = ThemeHelper.GetBrush(ThemeResourceKeys.Surface1, Color.FromRgb(69, 71, 90));
        var splitter = new GridSplitter
        {
            Background          = normalBrush,
            ShowsPreview        = false,
            ResizeBehavior      = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = orientation == Orientation.Horizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
            VerticalAlignment   = orientation == Orientation.Vertical   ? VerticalAlignment.Stretch  : VerticalAlignment.Center,
            Width               = orientation == Orientation.Vertical   ? 4 : double.NaN,
            Height              = orientation == Orientation.Horizontal ? 4 : double.NaN,
        };
        splitter.MouseEnter += (_, _) => splitter.Background = hoverBrush;
        splitter.MouseLeave += (_, _) => splitter.Background = normalBrush;
        return splitter;
    }

    private static IEnumerable<Guid> GetAllLeafIds(LayoutNode node)
    {
        if (node is LeafNode l) { yield return l.PaneId; yield break; }
        if (node is SplitNode s)
        {
            foreach (var id in GetAllLeafIds(s.First))  yield return id;
            foreach (var id in GetAllLeafIds(s.Second)) yield return id;
        }
    }
}
