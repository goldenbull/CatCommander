using System;
using Metalama.Patterns.Observability;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for one MainPanel (one side of the dual-pane view). Empty for now - the file
/// listing/browsing logic is a later milestone. Exists so the shortcut system and active-panel
/// tracking have something real to wire up to.
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
}
