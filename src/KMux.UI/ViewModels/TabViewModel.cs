using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMux.Core.Models;
using KMux.Layout;
using KMux.Macro;

namespace KMux.UI.ViewModels;

public partial class TabViewModel : ObservableObject, IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();

    public bool IsDashboard { get; }

    [ObservableProperty] private string _title = "Shell";
    [ObservableProperty] private bool   _isActive;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _isClaudeBusy;
    [ObservableProperty] private LayoutNode _layoutRoot;
    [ObservableProperty] private Guid   _activePaneId;

    private readonly Dictionary<Guid, PaneViewModel> _panes = new();
    private readonly ObservableCollection<PaneViewModel> _allPanes = new();
    public ReadOnlyObservableCollection<PaneViewModel> AllPanes { get; }
    private readonly ShellProfile _defaultProfile;
    private readonly MacroRecorder _recorder;
    private readonly LayoutTree _layoutTree;

    public TabViewModel(ShellProfile profile, MacroRecorder recorder)
    {
        AllPanes        = new ReadOnlyObservableCollection<PaneViewModel>(_allPanes);
        _defaultProfile = profile;
        _recorder       = recorder;
        _layoutTree     = new LayoutTree();

        // Create first pane — inject KMUX_PANE_ID so Claude Code hooks can identify the pane
        var firstLeaf    = (LeafNode)_layoutTree.Root;
        var paneProfile  = profile.Clone();
        paneProfile.EnvironmentVariables["KMUX_PANE_ID"] = firstLeaf.PaneId.ToString();
        var pane         = new PaneViewModel(firstLeaf.PaneId, paneProfile, recorder);
        _panes[firstLeaf.PaneId] = pane;
        _allPanes.Add(pane);
        SubscribePane(pane);
        ActivePaneId = firstLeaf.PaneId;
        LayoutRoot   = _layoutTree.Root;
    }

    // Dashboard-only constructor — no panes, no terminal processes
    private TabViewModel(bool isDashboard)
    {
        AllPanes        = new ReadOnlyObservableCollection<PaneViewModel>(_allPanes);
        IsDashboard     = true;
        _title          = "Dashboard";
        _defaultProfile = ShellProfile.Cmd;
        _recorder       = new MacroRecorder();
        _layoutTree     = new LayoutTree();
        _layoutRoot     = _layoutTree.Root;
        _activePaneId   = Guid.Empty;
    }

    public static TabViewModel CreateDashboard() => new(isDashboard: true);

    /// <summary>Restore a tab from a saved layout with per-pane profile selection.</summary>
    public TabViewModel(ShellProfile defaultProfile, MacroRecorder recorder,
                        KMux.Layout.LayoutNode layoutRoot,
                        IReadOnlyDictionary<Guid, KMux.Core.Models.PaneInfo> paneInfos,
                        Func<KMux.Core.Models.PaneInfo, ShellProfile> profileFactory)
    {
        AllPanes        = new ReadOnlyObservableCollection<PaneViewModel>(_allPanes);
        _defaultProfile = defaultProfile;
        _recorder       = recorder;
        _layoutTree     = new LayoutTree(layoutRoot);

        Guid firstId = Guid.Empty;
        foreach (var paneId in _layoutTree.GetAllPaneIds())
        {
            if (firstId == Guid.Empty) firstId = paneId;
            ShellProfile profile;
            if (paneInfos.TryGetValue(paneId, out var info))
                profile = profileFactory(info);
            else
                profile = defaultProfile.Clone();
            profile.EnvironmentVariables["KMUX_PANE_ID"] = paneId.ToString();
            var pane = new PaneViewModel(paneId, profile, recorder);
            _panes[paneId] = pane;
            _allPanes.Add(pane);
            SubscribePane(pane);
        }

        ActivePaneId = firstId;
        LayoutRoot   = _layoutTree.Root;
    }

    public PaneViewModel? GetPane(Guid paneId)
        => _panes.TryGetValue(paneId, out var vm) ? vm : null;

    /// <summary>Full CWD of the active pane — shown on tab hover.</summary>
    public string ToolTipText => GetPane(ActivePaneId)?.WorkingDirectory ?? "";

    partial void OnActivePaneIdChanged(Guid value) => OnPropertyChanged(nameof(ToolTipText));

    public void SplitPane(Guid paneId, SplitDirection dir)
    {
        var newId = _layoutTree.SplitPane(paneId, dir);
        var workingDir = _panes.TryGetValue(paneId, out var srcPane)
            ? srcPane.WorkingDirectory
            : _defaultProfile.WorkingDir;
        var profile = _defaultProfile.WithWorkingDir(workingDir);
        profile.EnvironmentVariables["KMUX_PANE_ID"] = newId.ToString();
        var pane    = new PaneViewModel(newId, profile, _recorder);
        _panes[newId] = pane;
        _allPanes.Add(pane);
        SubscribePane(pane);
        ActivePaneId  = newId;
        LayoutRoot    = _layoutTree.Root;
        _recorder.RecordNewPane(dir);
    }

    public void ClosePane(Guid paneId)
    {
        if (_panes.Count <= 1) return;   // don't close last pane in tab

        var pane = _panes[paneId];
        UnsubscribePane(pane);
        pane.Dispose();
        _panes.Remove(paneId);
        _allPanes.Remove(pane);
        _layoutTree.ClosePane(paneId);

        if (ActivePaneId == paneId)
            ActivePaneId = _panes.Keys.First();

        RefreshIsBusy();
        LayoutRoot = _layoutTree.Root;
    }

    public void UpdateSplitRatio(Guid paneId, double ratio)
    {
        _layoutTree.ResizePane(paneId, ratio);
        LayoutRoot = _layoutTree.Root;
    }

    public Guid? GetAdjacentPane(KMux.Layout.NavigationDirection dir)
        => _layoutTree.GetAdjacentPane(ActivePaneId, dir);

    public void FocusPane(Guid paneId)
    {
        foreach (var p in _panes.Values)
            p.IsFocused = p.PaneId == paneId;
        ActivePaneId = paneId;
    }

    private void SubscribePane(PaneViewModel pane)
    {
        pane.PropertyChanged += OnPanePropertyChanged;
        pane.ProcessExited   += OnPaneProcessExited;
    }

    private void UnsubscribePane(PaneViewModel pane)
    {
        pane.PropertyChanged -= OnPanePropertyChanged;
        pane.ProcessExited   -= OnPaneProcessExited;
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PaneViewModel.IsActive)
                           or nameof(PaneViewModel.IsClaudeBusy))
            RefreshIsBusy();
    }

    private void OnPaneProcessExited(object? sender, EventArgs e)
    {
        if (sender is not PaneViewModel pane) return;
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => ClosePane(pane.PaneId));
    }

    private void RefreshIsBusy()
    {
        IsBusy       = _panes.Values.Any(p => p.IsActive);
        IsClaudeBusy = _panes.Values.Any(p => p.IsClaudeBusy);
    }

    public event EventHandler? Disposing;

    public void Dispose()
    {
        Disposing?.Invoke(this, EventArgs.Empty);
        foreach (var p in _panes.Values) { UnsubscribePane(p); p.Dispose(); }
        _panes.Clear();
        _allPanes.Clear();
    }
}
