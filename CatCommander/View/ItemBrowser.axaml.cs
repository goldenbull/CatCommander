using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class ItemBrowser : UserControl
{
    private ItemBrowserViewModel? _viewModel;

    // Keyed by the row *control* (recycled/reused across different FileItemRow models as the grid
    // scrolls), not the model - see OnRowPrepared/OnRowClearing.
    private readonly Dictionary<TreeDataGridRow, (FileItemRow Model, PropertyChangedEventHandler Handler)> _markedSubscriptions = new();

    /// <summary>
    /// Which of this control's own key-handling concerns currently owns the keyboard - the two
    /// things ItemBrowser intercepts keys for below (quick filter editing, F2's in-place rename
    /// box) are mutually exclusive by construction, because real Avalonia focus is exclusive:
    /// exactly one control has it. GetFocusScope() is the single place that's resolved; everything
    /// below dispatches off its result instead of each re-deriving its own "is this really for me"
    /// check (which used to be two separate, easy-to-desync predicates - see git history).
    /// </summary>
    private enum FocusScope
    {
        Grid,
        RenameBox,
        Other,
    }

    // Table-driven key handling, one small dictionary per FocusScope - adding a new in-place
    // editing surface later means adding a new FocusScope case and a new dictionary here, not a
    // new branch inside OnPreviewKeyDown alongside unrelated existing ones. Built once, but the
    // closures reference the mutable _viewModel field rather than capturing an instance - FileGrid
    // outlives any single ItemBrowserViewModel (one ItemBrowser per panel, reused across every
    // tab - see MainPanelViewModel's doc comment).
    private readonly Dictionary<Key, Action> _gridFilterKeyHandlers;
    private readonly Dictionary<Key, Action> _renameBoxKeyHandlers;

    public ItemBrowser()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        _gridFilterKeyHandlers = new Dictionary<Key, Action>
        {
            [Key.Back] = () => _viewModel?.RemoveLastFilterCharacter(),
            [Key.Escape] = () => _viewModel?.ClearFilter(),
        };
        _renameBoxKeyHandlers = new Dictionary<Key, Action>
        {
            [Key.Enter] = () => _viewModel?.CommitActiveRename(),
            [Key.Escape] = () => _viewModel?.CancelActiveRename(),
        };

        // Keeps each realized row's "marked" CSS class (see ItemBrowser.axaml's
        // TreeDataGridRow.marked styles) in sync with its model's FileItemRow.IsMarked - nothing in
        // XAML can target a row control directly, since TreeDataGrid synthesizes them from its own
        // template rather than this file declaring them.
        FileGrid.RowPrepared += OnRowPrepared;
        FileGrid.RowClearing += OnRowClearing;

        // Mouse double-click: enters a directory, or hands a file to the OS's own default handler
        // (Finder/Explorer double-click behavior) - see ItemBrowserViewModel.OpenOrEnterCurrentItem.
        FileGrid.DoubleTapped += OnFileGridDoubleTapped;

        // Quick filter: both installed Tunnel-phase on this UserControl, so they run after
        // ShortcutRouter's own Window-level Tunnel handler (giving bound Operations first
        // refusal - none currently claim Backspace/Escape/plain character keys) but before
        // FileGrid's own Direct/Bubble handling, which is what OnPreviewTextInput needs to
        // preempt - TreeDataGrid has its own built-in TextInput handling that jumps to the next
        // row starting with the typed letter (see TreeDataGridRowSelectionModel.HandleTextInput),
        // which would otherwise fire instead of/alongside the filter.
        AddHandler(TextInputEvent, OnPreviewTextInput, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // Permanent, not a one-shot subscription: FileGrid is created once per panel and reused
        // for every tab (see MainPanelViewModel's doc comment) - swapping Source only rebinds
        // data, it never moves or resizes FileGrid itself. That's exactly why TreeDataGrid's own
        // Star-width recompute (normally driven by Avalonia's EffectiveViewportChanged, which only
        // fires on a genuine *geometric* viewport change) never fires again for a Source swap: the
        // new ColumnList's internal viewport width is never told what it actually is, since
        // nothing about FileGrid's own geometry changed. Feed it the rows ScrollViewer's actual
        // viewport, not FileGrid.Bounds: the latter includes the grid border and any vertical
        // scrollbar, making Star columns a few pixels too wide and falsely keeping the horizontal
        // Auto scrollbar visible even when all columns fit.
        FileGrid.LayoutUpdated += (_, _) =>
        {
            if (FileGrid.Source?.Columns is { } columns)
            {
                var rowsScrollViewer = FileGrid.GetVisualDescendants()
                    .OfType<ScrollViewer>()
                    .FirstOrDefault(viewer => viewer.Name == "PART_ScrollViewer");
                var viewport = rowsScrollViewer?.Viewport ?? default;
                if (viewport.Width <= 0)
                    return;

                columns.ViewportChanged(new Rect(viewport));
                columns.CommitActualWidths();
            }
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
            _viewModel.ScrollToRowRequested -= OnScrollToRowRequested;
        }

        _viewModel = DataContext as ItemBrowserViewModel;

        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
            _viewModel.ScrollToRowRequested += OnScrollToRowRequested;
            FocusGrid();
        }
    }

    private void OnFocusRequested() => FocusGrid();

    private void OnFileGridDoubleTapped(object? sender, TappedEventArgs e) => _viewModel?.OpenOrEnterCurrentItem();

    /// <summary>
    /// A row just got a (possibly new) FileItemRow model - sync its "marked" class immediately,
    /// and subscribe so a *later* IsMarked toggle (Space, on whichever row is current when it's
    /// pressed) updates this same row's class too, not just the state at realize time.
    /// </summary>
    private void OnRowPrepared(object? sender, TreeDataGridRowEventArgs e)
    {
        // TreeDataGrid reuses/mutates a single TreeDataGridRowEventArgs instance across every row
        // realize/clear (see TreeDataGrid.RaiseRowPrepared) - closing over the row *now*, into a
        // local, is what keeps the handler below pointed at the row this subscription was actually
        // set up for. Closing over `e.Row` directly instead would re-read it whenever the handler
        // *runs* (Space, potentially much later), by which point the shared event-args object may
        // already refer to a completely different row - a real bug caught via a headless test that
        // crashed with a NullReferenceException from exactly that stale read.
        var row = e.Row;
        if (row.Model is not FileItemRow model)
            return;

        UpdateMarkedClass(row, model);

        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == nameof(FileItemRow.IsMarked))
                UpdateMarkedClass(row, model);
        };
        model.PropertyChanged += handler;
        _markedSubscriptions[row] = (model, handler);
    }

    /// <summary>
    /// The row is being recycled for a different model (or unrealized entirely) - drop the
    /// subscription to the *old* model now, or it would keep firing (and leaking) indefinitely.
    /// </summary>
    private void OnRowClearing(object? sender, TreeDataGridRowEventArgs e)
    {
        if (_markedSubscriptions.Remove(e.Row, out var subscription))
            subscription.Model.PropertyChanged -= subscription.Handler;
    }

    private static void UpdateMarkedClass(TreeDataGridRow row, FileItemRow model) =>
        row.Classes.Set("marked", model.IsMarked);

    /// <summary>
    /// Printable characters typed while FileGrid effectively has focus feed the quick filter
    /// instead of TreeDataGrid's own built-in type-ahead row jump. Marking the event Handled here
    /// (Tunnel phase, before FileGrid's own Direct-phase OnTextInput override runs) is what
    /// actually suppresses that built-in behavior.
    /// </summary>
    private void OnPreviewTextInput(object? sender, TextInputEventArgs e)
    {
        if (_viewModel is null || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]) || GetFocusScope() != FocusScope.Grid)
            return;

        // Space is the mark/unmark toggle for the cursor row (Total Commander's multi-selection) -
        // but only when a filter isn't already being typed, where it's an AND-token separator
        // instead (see QuickFilter). Checking IsFilterActive, not "is this the first character",
        // is what lets a filter that already contains a space (e.g. "aa bb") keep accepting more
        // spaces normally.
        if (e.Text == " " && !_viewModel.IsFilterActive)
        {
            _viewModel.ToggleMarkCurrentItem();
            e.Handled = true;
            return;
        }

        _viewModel.AppendFilterText(e.Text);
        e.Handled = true;
    }

    /// <summary>
    /// Backspace/Escape editing of the quick filter (only while a filter is actually active), and
    /// Enter/Escape committing/cancelling F2's in-place rename box - both dispatched here off
    /// GetFocusScope(), at Tunnel phase on this UserControl, above FileGrid in the tree. Tunnel
    /// dispatch runs root-to-leaf, so this always runs before FileGrid's own built-in Tunnel-phase
    /// key handling (arrow-key/type-ahead navigation) can consume the same key first - which it
    /// otherwise would, since marking a Tunnel-phase event Handled stops the Bubble phase
    /// (leaf-to-root) from ever starting, meaning a handler on the edit box itself would never even
    /// run.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null)
            return;

        var handlers = GetFocusScope() switch
        {
            FocusScope.RenameBox => _renameBoxKeyHandlers,
            FocusScope.Grid when _viewModel.IsFilterActive => _gridFilterKeyHandlers,
            _ => null,
        };

        if (handlers is null || !handlers.TryGetValue(e.Key, out var handler))
            return;

        handler();
        e.Handled = true;
    }

    /// <summary>
    /// The single canonical answer to "what does real Avalonia keyboard focus mean for this
    /// control's own key interception" - checked against FocusManager rather than the routed
    /// event's Source, since a mouse click into a cell can move real focus to that
    /// TreeDataGridCell (Focusable by default) rather than FileGrid itself.
    /// </summary>
    private FocusScope GetFocusScope()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

        if (focused is TextBox)
        {
            // F2's in-place rename box (see ItemBrowserViewModel.CreateNameColumn) is itself a
            // visual descendant of FileGrid - checked first so it lands on RenameBox rather than
            // falling through to the Grid case below, which would hijack its typing (including its
            // own Space characters) as quick-filter/mark-toggle input. Any other focused TextBox
            // (the path address bar, the history flyout's ListBox) is FileGrid's sibling, not
            // descendant, and correctly falls to Other.
            return (focused as Visual)?.FindAncestorOfType<TreeDataGrid>() == FileGrid
                ? FocusScope.RenameBox
                : FocusScope.Other;
        }

        return focused == FileGrid || (focused as Visual)?.FindAncestorOfType<TreeDataGrid>() == FileGrid
            ? FocusScope.Grid
            : FocusScope.Other;
    }

    /// <summary>
    /// GotoFirstItem/GotoLastItem/GoBackToParentFolder's restore/a fresh listing's default all set
    /// the current row directly via ItemBrowserViewModel.SetCurrentRow, bypassing TreeDataGrid's
    /// own keyboard handling entirely - which is the *only* thing that normally scrolls a newly
    /// current row into view (TreeDataGridRowSelectionModel.MoveSelection calls
    /// RowsPresenter.BringIntoView internally, but only from its own OnKeyDown). Without this, Home/
    /// End moved the selection correctly but left the scroll position wherever it already was.
    /// </summary>
    private int? _pendingScrollRowIndex;

    private void OnScrollToRowRequested(int rowIndex)
    {
        // Coalesced through a shared field rather than each call posting its own closure over
        // rowIndex - a rapid burst (e.g. Home/End key-repeat) queues multiple posts, and only the
        // latest target actually needs to run; every queued post reads this field at *its own*
        // run time; not a captured value, so whichever runs first consumes it and any later,
        // now-redundant post becomes a no-op.
        _pendingScrollRowIndex = rowIndex;
        Dispatcher.UIThread.Post(() =>
        {
            if (_pendingScrollRowIndex is not { } target)
                return;

            _pendingScrollRowIndex = null;
            FileGrid.RowsPresenter?.BringIntoView(target);

            // BringIntoView can recycle an already-realized row (one that was scrolled out of
            // view) into the target index rather than realizing a fresh one - that recycle path
            // repositions it (RowIndex updated) but doesn't re-check selection, since the
            // SelectedIndex change that made this row "the current one" already ran synchronously,
            // *before* this deferred callback, when this row wasn't part of the realized window
            // yet. Left alone, it can end up sitting at the right index while still showing
            // whichever selection state its previous occupant had - confirmed via targeted
            // logging under rapid Home/End: the row's own RowIndex and the selection model's
            // SelectedIndex agreed, but TreeDataGridRow.IsSelected was still stale. Forcing a
            // resync here closes that gap regardless of which path realized the row.
            FileGrid.RefreshRowSelection(target);
        });
    }

    /// <summary>
    /// The single place real Avalonia keyboard focus gets (re)synced to ViewModel-driven
    /// activation - see ItemBrowserViewModel.FocusRequested for the full list of triggers
    /// (navigation, tab switch, panel switch/SwitchPanel, a new OpenSelectedFolderInNewTab tab).
    ///
    /// Always deferred one dispatcher tick, on purpose: SwitchPanel is bound to plain Tab, and
    /// Avalonia has its own built-in Tab-key focus navigation that is *not* suppressed by marking
    /// the KeyDownEvent Handled (it's an accessibility guarantee, not app-overridable) - it runs
    /// later in the same synchronous key-down dispatch as ShortcutRouter's Tunnel handler. Calling
    /// Focus() synchronously from within that dispatch wins the race only to immediately lose it
    /// to Avalonia's own navigation moving focus somewhere else before the key-down finishes.
    /// Posting instead guarantees we run strictly after the entire key-down dispatch (default
    /// navigation included) has settled, so we always get the last word regardless of which
    /// gesture triggered this.
    ///
    /// Whether FileGrid is already attached only changes what we're waiting to be true before
    /// posting: if it's already part of a rooted visual tree (true for every case now except the
    /// very first ItemBrowser attachment per panel - see MainPanelViewModel), post right away; if
    /// it's still being constructed this frame, wait for AttachedToVisualTree first.
    /// </summary>
    private void FocusGrid()
    {
        if (FileGrid.IsAttachedToVisualTree())
        {
            Dispatcher.UIThread.Post(() => FileGrid.Focus());
        }
        else
        {
            FileGrid.AttachedToVisualTree += OnFileGridAttached;
        }
    }

    private void OnFileGridAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        FileGrid.AttachedToVisualTree -= OnFileGridAttached;
        Dispatcher.UIThread.Post(() => FileGrid.Focus());
    }

    // ListBox.SelectedItem isn't bound to anything - the history list has no notion of a
    // "currently selected" entry between openings, it's a one-shot pick. Handling the click here
    // (rather than a Command) is what lets the flyout close itself immediately, and clearing
    // SelectedItem afterward is what lets picking the *same* entry again next time still raise
    // SelectionChanged (a ListBox doesn't re-fire it for reselecting the already-selected item).
    private void OnHistorySelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is string path)
        {
            _ = _viewModel?.NavigateToAsync(path);
            HistoryButton.Flyout?.Hide();
        }

        HistoryList.SelectedItem = null;
    }

    // Dismissing a Flyout (click outside, Escape, or picking an entry - Hide() above also raises
    // this) doesn't return keyboard focus to whatever had it before the Flyout opened; Avalonia
    // just drops it. Without this, arrow keys stopped doing anything after closing the history
    // dropdown until the user clicked the grid again.
    private void OnHistoryFlyoutClosed(object? sender, EventArgs e) => FocusGrid();
}
