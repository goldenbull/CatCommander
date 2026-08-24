using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CatCommander.QuickAccess;
using Metalama.Patterns.Observability;
using ReactiveUI;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for one MainPanel (one side of the dual-pane view): a quick access row and a
/// tab strip of ItemBrowserViewModel content.
/// </summary>
[Observable]
public partial class MainPanelViewModel
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
    public ItemBrowserViewModel? ActiveTab { get; set; }

    public ICommand NavigateToQuickAccessCommand { get; }

    public MainPanelViewModel(Func<ItemBrowserViewModel> itemBrowserFactory)
    {
        NavigateToQuickAccessCommand = ReactiveCommand.Create<QuickAccessEntry>(entry =>
            _ = ActiveTab?.NavigateToAsync(entry.Path));

        // Just one tab to start - old-ref opened two "for testing", which isn't a real default.
        var tab = itemBrowserFactory();
        Tabs.Add(tab);
        ActiveTab = tab;
        _ = tab.NavigateToAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }
}
