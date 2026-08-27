using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CatCommander.Config;
using CatCommander.QuickAccess;
using CatCommander.Resources;
using CatCommander.Shortcuts;
using Metalama.Patterns.Observability;
using ReactiveUI;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for one MainPanel (one side of the dual-pane view): a quick access row, a strip of
/// tab-header buttons, and the backend data (Tabs) they select between.
///
/// Deliberately *not* a TabControl-per-tab model: each Tabs entry is a long-lived
/// ItemBrowserViewModel that keeps its own directory listing/selection state alive even while not
/// shown (so it can be refreshed/watched in the background later without needing to be the visible
/// tab), and MainPanel's View holds exactly one ItemBrowser/TreeDataGrid per panel, permanently -
/// switching tabs only rebinds its DataContext to a different Tabs entry, it never
/// constructs/destroys the grid. This is also what fixed newly-created tabs getting a TreeDataGrid
/// that was never laid out yet (its Star-width Name column stuck at minimum width): the grid a new
/// tab displays into is always the same already-laid-out, already-focusable control the first tab
/// used, never a freshly attached one.
/// </summary>
[Observable]
public partial class MainPanelViewModel : IShortcutCommandSource
{
    /// <summary>
    /// Whether this is MainWindowViewModel.ActivePanel right now - set by MainWindowViewModel,
    /// not computed locally, since only it knows about both panels. Drives the active-panel
    /// highlight in MainPanel's View.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Invoked by the View when this panel gains focus (click, or SwitchPanel), so
    /// MainWindowViewModel can track ActivePanel without MainPanelViewModel needing to know
    /// about its parent window.
    /// </summary>
    [NotObservable]
    public Action? OnActivated { get; set; }

    public IReadOnlyList<QuickAccessEntry> QuickAccessEntries { get; } = QuickAccessService.GetEntries();
    public ObservableCollection<ItemBrowserViewModel> Tabs { get; } = new();
    public ItemBrowserViewModel? ActiveTab { get; private set; }

    public ICommand NavigateToQuickAccessCommand { get; }
    public ICommand SelectTabCommand { get; }

    private readonly Func<ItemBrowserViewModel> _itemBrowserFactory;
    private readonly Dictionary<Operation, ICommand> _commands;

    public MainPanelViewModel(Func<ItemBrowserViewModel> itemBrowserFactory)
    {
        _itemBrowserFactory = itemBrowserFactory;

        NavigateToQuickAccessCommand = ReactiveCommand.Create<QuickAccessEntry>(entry =>
            _ = ActiveTab?.NavigateToAsync(entry.Path));
        SelectTabCommand = ReactiveCommand.Create<ItemBrowserViewModel>(SetActiveTab);

        _commands = new Dictionary<Operation, ICommand>
        {
            [Operation.OpenSelectedFolderInNewTab] = ReactiveCommand.Create(OpenSelectedFolderInNewTab),
            [Operation.SwitchTabInSamePanel] = ReactiveCommand.Create(SwitchTabInSamePanel),
            [Operation.CloseTab] = ReactiveCommand.Create(CloseTab),
        };

        // Just one tab to start - old-ref opened two "for testing", which isn't a real default.
        var tab = itemBrowserFactory();
        Tabs.Add(tab);
        SetActiveTab(tab);
        _ = tab.NavigateToAsync(HomePath);
    }

    // Shared with CloseTab's last-tab fallback below - both mean "what a brand new tab starts at".
    private static string HomePath => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // The one place ActiveTab is ever assigned - keeps IsActiveTab (the tab-header button's
    // selected look) in sync with it, the same relationship MainWindowViewModel.SetActivePanel
    // keeps between ActivePanel and IsActive.
    private void SetActiveTab(ItemBrowserViewModel tab)
    {
        if (ActiveTab is not null)
            ActiveTab.IsActiveTab = false;

        ActiveTab = tab;
        tab.IsActiveTab = true;
    }

    // Panel-scoped, not tab-scoped: needs to add a *sibling* to ActiveTab, which ItemBrowserViewModel
    // itself has no way to reach (it doesn't know about the Tabs collection it lives in).
    private void OpenSelectedFolderInNewTab()
    {
        var resource = ActiveTab?.GetSelectedEnterableResource();
        if (resource is not null)
            OpenNewTab(resource.Value);
    }

    /// <summary>
    /// Opens path in a brand new tab, activating it. Public (unlike the Operations above) because
    /// MainWindowViewModel's directional OpenCurrentFolderInPanel calls it across panels - it already
    /// holds direct references to both, the same access OpenSelectedFolderInNewTab doesn't need
    /// since it only ever targets its own panel.
    /// </summary>
    public void OpenNewTab(string path)
    {
        var tab = _itemBrowserFactory();
        Tabs.Add(tab);
        SetActiveTab(tab);
        _ = tab.NavigateToAsync(path);
    }

    public void OpenNewTab(ResourceRef resource)
    {
        var tab = _itemBrowserFactory();
        Tabs.Add(tab);
        SetActiveTab(tab);
        _ = tab.NavigateToAsync(resource);
    }

    public PanelSessionState CaptureSession()
    {
        var paths = Tabs.Select(tab => tab.SessionPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();
        var activePath = ActiveTab?.SessionPath;
        return new PanelSessionState
        {
            Tabs = paths,
            ActiveTab = activePath is null ? 0 : Math.Max(0, paths.IndexOf(activePath)),
        };
    }

    public void RestoreSession(PanelSessionState state)
    {
        var paths = state.Tabs.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        if (paths.Count == 0)
            return;

        // Reuse the constructor-created tab. Its startup Home navigation is cancelled by the
        // restored navigation through ItemBrowserViewModel's generation/CTS mechanism.
        var first = Tabs[0];
        _ = first.NavigateToAsync(paths[0]);
        for (var i = 1; i < paths.Count; i++)
        {
            var tab = _itemBrowserFactory();
            Tabs.Add(tab);
            _ = tab.NavigateToAsync(paths[i]);
        }

        SetActiveTab(Tabs[Math.Clamp(state.ActiveTab, 0, Tabs.Count - 1)]);
    }

    // A panel always has at least one tab (see the constructor) - closing the only one would leave
    // the panel with nothing to show, so it's reset to HomePath in place instead of being removed.
    // Otherwise, the sibling to its left (or right, if it was the first tab) becomes active before
    // the closed tab is actually removed and its Source disposed - RebuildSource already handles
    // disposing a tab's *own* Source on every navigation, but this is the first path that removes
    // a tab outright, so its last Source needs the same cleanup here.
    private void CloseTab()
    {
        if (ActiveTab is not { } closedTab)
            return;

        if (Tabs.Count == 1)
        {
            _ = closedTab.NavigateToAsync(HomePath);
            return;
        }

        var closedIndex = Tabs.IndexOf(closedTab);
        var siblingIndex = closedIndex > 0 ? closedIndex - 1 : closedIndex + 1;
        var newActiveTab = Tabs[siblingIndex];

        Tabs.Remove(closedTab);
        SetActiveTab(newActiveTab);
        (closedTab.Source as IDisposable)?.Dispose();
    }

    // Deliberately always Ctrl+Tab, even on macOS (see ShortcutsSettings.GetDefaults) - Cmd+Tab is
    // macOS's own system-wide app switcher, a true OS-level reservation.
    private void SwitchTabInSamePanel()
    {
        if (Tabs.Count < 2)
            return;

        var index = ActiveTab is null ? -1 : Tabs.IndexOf(ActiveTab);
        SetActiveTab(Tabs[(index + 1) % Tabs.Count]);
    }

    // Forwards to whichever ItemBrowserViewModel.FocusRequested the View already listens to - see
    // that event's doc comment. Called by MainWindowViewModel.SetActivePanel, so SwitchPanel (Tab)
    // and a mouse click activating this panel both actually move keyboard focus, not just the
    // IsActive flag the active-panel visual indicator reads.
    public void RequestFocus() => ActiveTab?.RequestFocus();

    public ICommand? GetCommand(Operation operation) => _commands.GetValueOrDefault(operation);
}
