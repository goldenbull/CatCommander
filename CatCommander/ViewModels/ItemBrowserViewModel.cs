using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Threading;
using CatCommander.Browsing;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Resources;
using CatCommander.Services;
using CatCommander.Shortcuts;
using CatCommander.Utils;
using Metalama.Patterns.Observability;
using NLog;
using ReactiveUI;

namespace CatCommander.ViewModels;

public enum ItemBrowserViewMode
{
    List,
    TreeList,
}

/// <summary>
/// One tab's content: address bar + file list/tree + selection summary. Talks to whatever
/// IFileSystemProvider FileSystemProviderRegistry resolves CurrentPath to - currently always
/// LocalFileSystemProvider, but nothing here assumes that.
/// </summary>
[Observable]
public partial class ItemBrowserViewModel : IShortcutCommandSource
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private readonly FileSystemProviderRegistry _providers;
    private readonly IconCache _iconCache;
    private readonly ITerminalLauncher? _terminalLauncher;
    private readonly Dictionary<Operation, ICommand> _commands;

    private IFileSystemProvider? _provider;
    private IReadOnlyList<IFileSystemItem> _allItems = Array.Empty<IFileSystemItem>();
    private IReadOnlyList<BrowserItem> _browserItems = Array.Empty<BrowserItem>();
    [NotObservable]
    private CancellationTokenSource? _navigationCts;

    [NotObservable]
    private int _navigationGeneration;

    // Provider loads are intentionally concurrent/cancellable, but committing their results is a
    // single-writer operation. TreeDataGrid's selection model is not safe to dispose/reconstruct
    // concurrently, even when both callers passed the generation check moments earlier.
    [NotObservable]
    private readonly object _navigationCommitGate = new();

    /// <summary>
    /// The current listing's root-level rows (one FileItemRow per _allItems entry). Kept alive
    /// across filter changes so ApplyVisibility can just pick a subset of these into Source.Items
    /// rather than rebuilding rows from scratch (which would re-trigger every FileItemRow's async
    /// icon load per keystroke). Rebuilt only by RebuildSource - i.e. on navigation and view-mode
    /// toggles.
    /// </summary>
    private List<FileItemRow> _rows = new();

    /// <summary>
    /// The provider currently resolved for CurrentPath - exposed so MainWindowViewModel.
    /// Retained for directory/tree compatibility. Projected listings use each FileItemRow's own
    /// BrowserItem.Resource.Provider instead, and transfers go through ResourceTransferService.
    /// </summary>
    public IFileSystemProvider? Provider => _provider;
    public BrowserContext? Context { get; private set; }
    public ContainerRef? WritableDestination => Context?.WritableDestination;

    /// <summary>A stable provider path suitable for restoring this tab on the next launch.</summary>
    [NotObservable]
    public string? SessionPath => Context?.Kind == ListingKind.Directory
        ? Context.Location?.Path
        : SelectionModel?.SelectedItem?.BrowserItem.Container?.Path;

    public string CurrentPath { get; set; } = string.Empty;
    public ItemBrowserViewMode ViewMode { get; set; } = ItemBrowserViewMode.List;
    public ITreeDataGridSource<FileItemRow>? Source { get; private set; }

    /// <summary>
    /// Total Commander-style quick filter text - see ApplyVisibility. Reset to empty by every
    /// RebuildSource (a new folder listing starts unfiltered); otherwise only ever changed via
    /// AppendFilterText/RemoveLastFilterCharacter/ClearFilter, called by the View in response to
    /// characters typed while FileGrid has focus (see ItemBrowser.axaml.cs).
    /// </summary>
    public string FilterText { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the quick filter bar should be shown below the grid - see ItemBrowser.axaml.
    /// Maintained explicitly alongside FilterText (in SetFilterText) rather than as a computed
    /// FilterText.Length > 0 property - Metalama's Observable aspect wasn't reliably raising
    /// PropertyChanged for a get-only property derived from a private-set one across a keystroke
    /// burst (the bar never appeared despite FilterText being set), so this avoids depending on
    /// that dependency inference for something the UI actually needs to react to.
    /// </summary>
    public bool IsFilterActive { get; private set; }

    /// <summary>
    /// Cmd/Ctrl+. (ToggleHiddenFiles) - whether dotfiles/the OS Hidden attribute (see
    /// LocalFileSystemProvider.IsHiddenEntry) are included in the listing. Unlike FilterText/
    /// IsFilterActive, this deliberately survives RebuildSource (navigating to a new folder) - a
    /// "show hidden files" preference is sticky for the whole tab, the way Finder/Explorer/Total
    /// Commander's own equivalents are, not scoped to one directory listing.
    /// </summary>
    public bool ShowHiddenFiles { get; private set; }

    /// <summary>
    /// Paths visited via NavigateToAsync, most-recent-first, backing the address bar's history
    /// dropdown. Only recorded when the resolved provider's TracksHistory says so (real,
    /// independently-typeable roots - the local file system, later an SFTP session) - a future
    /// archive provider's path-inside-an-archive navigation, or Ctrl+B's in-place tree expansion
    /// (which doesn't call NavigateToAsync at all), never lands here. Revisiting a path moves it
    /// back to the front rather than duplicating it.
    /// </summary>
    public ObservableCollection<string> NavigationHistory { get; } = new();

    private const int MaxHistoryEntries = 50;

    /// <summary>
    /// Whether this is MainPanelViewModel.ActiveTab right now - set by MainPanelViewModel.SetActiveTab,
    /// not computed locally, same reasoning as MainPanelViewModel.IsActive. Drives the tab-header
    /// button's selected look in MainPanel's View.
    /// </summary>
    public bool IsActiveTab { get; set; }

    public int TotalFileCount { get; private set; }
    public int TotalFolderCount { get; private set; }
    public long TotalSize { get; private set; }
    public int SelectedFileCount { get; private set; }
    public int SelectedFolderCount { get; private set; }
    public long SelectedSize { get; private set; }

    public string SelectionSummary =>
        $"Selected {FileItemModel.FormatFileSize(SelectedSize)} / {FileItemModel.FormatFileSize(TotalSize)}, " +
        $"{SelectedFileCount} / {TotalFileCount} files, {SelectedFolderCount} / {TotalFolderCount} folders";

    public ICommand ToggleViewModeCommand { get; }

    /// <summary>
    /// Raised whenever this tab's Source is torn down and rebuilt (RebuildSource - every
    /// navigation, and view-mode toggling) or this tab is otherwise handed real activation
    /// (MainPanelViewModel.RequestFocus, from SwitchPanel/a mouse click activating the panel).
    /// None of those move actual Avalonia keyboard focus on their own - the View (ItemBrowser)
    /// subscribes to this to put focus back in the grid, since without it focus is left to
    /// whatever FocusManager's own fallback search lands on once the previously-focused element
    /// is torn down (observed: the toolbar's first button), not wherever the user is working.
    /// </summary>
    public event Action? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke();

    /// <summary>
    /// Raised with a row index whenever the current item changes programmatically (GotoFirstItem/
    /// GotoLastItem/GoBackToParentFolder's restore, the new-listing default - see SetCurrentRow),
    /// as opposed to via TreeDataGrid's own keyboard/pointer handling. Only *that* internal
    /// handling scrolls the row into view on its own (see
    /// TreeDataGridRowSelectionModel.MoveSelection's RowsPresenter.BringIntoView call) - setting
    /// SelectedIndex directly, which is all any of the above do, never does. The View subscribes
    /// to this to call the same BringIntoView itself, since only it has a reference to FileGrid.
    /// </summary>
    public event Action<int>? ScrollToRowRequested;

    // The one place the current item is ever set to a specific row index - keeps selection and
    // "make sure it's actually visible" (ScrollToRowRequested above) together.
    //
    // Setting SelectedIndex, not calling Select(): the selection model is multi-select
    // (SingleSelect = false, for future multi-file operations), and Select() only *adds* to
    // whatever's already selected - once anything is selected, a later Select() call doesn't move
    // SelectedItem/SelectedIndex at all, it just grows the selection. SelectedIndex's setter
    // (Clear() + Select()) is the one that actually replaces the current item, which matters the
    // moment more than one caller sets the current row during the same navigation (e.g.
    // GoBackToParentFolder's default-then-restore).
    private void SetCurrentRow(int rowIndex)
    {
        if (SelectionModel is { } selectionModel)
            selectionModel.SelectedIndex = new IndexPath(rowIndex);

        ScrollToRowRequested?.Invoke(rowIndex);
    }

    public ICommand NavigateToHistoryEntryCommand { get; }

    public ItemBrowserViewModel(
        FileSystemProviderRegistry providers,
        IconCache iconCache,
        ITerminalLauncher? terminalLauncher = null)
    {
        _providers = providers;
        _iconCache = iconCache;
        _terminalLauncher = terminalLauncher;

        ToggleViewModeCommand = ReactiveCommand.Create(ToggleViewMode);
        NavigateToHistoryEntryCommand = ReactiveCommand.Create<string>(path => _ = NavigateToAsync(path));

        _commands = new Dictionary<Operation, ICommand>
        {
            [Operation.GoIntoCurrentFolder] = ReactiveCommand.Create(GoIntoCurrentFolder),
            [Operation.GoBackToParentFolder] = ReactiveCommand.Create(GoBackToParentFolder),
            [Operation.GotoFirstItem] = ReactiveCommand.Create(GotoFirstItem),
            [Operation.GotoLastItem] = ReactiveCommand.Create(GotoLastItem),
            [Operation.ReverseSelection] = ReactiveCommand.Create(ReverseSelection),
            [Operation.Rename] = ReactiveCommand.Create(BeginRenameCurrentItem),
            [Operation.Refresh] = ReactiveCommand.Create(RefreshCurrentFolder),
            [Operation.ToggleHiddenFiles] = ReactiveCommand.Create(ToggleHiddenFiles),
            [Operation.ExpandCurrentFolder] = ReactiveCommand.Create(ExpandCurrentFolder),
            [Operation.ExpandSelectedFolders] = ReactiveCommand.Create(ExpandSelectedFolders),
            [Operation.OpenTerminal] = ReactiveCommand.Create(OpenTerminal),
        };
    }

    private string? GetLocalShellDirectory()
    {
        var location = Context?.Location;
        return location is { } resource &&
               resource.Provider is ILocalShellContextProvider local
            ? local.GetLocalShellDirectory(resource)
            : null;
    }

    private void OpenTerminal()
    {
        if (GetLocalShellDirectory() is { } directory)
            _terminalLauncher?.Open(directory);
    }

    public Task NavigateToAsync(string path) =>
        NavigateCoreAsync(path, () => _providers.ResolveAsync(path));

    public Task NavigateToAsync(ResourceRef resource) =>
        NavigateCoreAsync(resource.Path, () => Task.FromResult((resource.Provider, resource.Path)));

    private async Task NavigateCoreAsync(
        string displayPath,
        Func<Task<(IFileSystemProvider Provider, string RelativePath)>> resolve)
    {
        var generation = Interlocked.Increment(ref _navigationGeneration);
        var navigationCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _navigationCts, navigationCts);
        previousCts?.Cancel();

        try
        {
            var (provider, relativePath) = await resolve();
            var listing = new DirectoryListingSource(provider, relativePath);
            await LoadListingAsync(listing, displayPath, generation, navigationCts);
        }
        catch (OperationCanceledException) when (navigationCts.IsCancellationRequested)
        {
            // Superseded by a newer navigation in this tab.
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to navigate to {0}", displayPath);
        }
        finally
        {
            Interlocked.CompareExchange(ref _navigationCts, null, navigationCts);
            navigationCts.Dispose();
        }
    }

    private async Task NavigateToListingAsync(IListingSource listing, string displayPath)
    {
        var generation = Interlocked.Increment(ref _navigationGeneration);
        var navigationCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _navigationCts, navigationCts);
        previousCts?.Cancel();

        try
        {
            await LoadListingAsync(listing, displayPath, generation, navigationCts);
        }
        catch (OperationCanceledException) when (navigationCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load {0} listing", listing.Kind);
        }
        finally
        {
            Interlocked.CompareExchange(ref _navigationCts, null, navigationCts);
            navigationCts.Dispose();
        }
    }

    private async Task LoadListingAsync(
        IListingSource listing,
        string displayPath,
        int generation,
        CancellationTokenSource navigationCts)
    {
            var snapshot = await listing.LoadAsync(navigationCts.Token);

            lock (_navigationCommitGate)
            {
                // Recheck only after obtaining the single-writer gate. Without that ordering, two
                // completed loads can both pass the check and overlap Source disposal/recreation.
                if (generation != Volatile.Read(ref _navigationGeneration) || navigationCts.IsCancellationRequested)
                    return;

                _provider = listing.Location?.Provider;
                CurrentPath = displayPath;
                Context = new BrowserContext(listing);
                _browserItems = listing.Kind == ListingKind.Directory ? Sort(snapshot.Items) : snapshot.Items;
                _allItems = _browserItems.Select(x => x.Item).ToList();

                // Search/branch results are already an explicitly ordered projection, not a directory
                // hierarchy for TreeDataGrid to discover again through ChildSelector.
                if (listing.Kind != ListingKind.Directory)
                    ViewMode = ItemBrowserViewMode.List;

                if (listing.Location?.Provider.TracksHistory == true)
                    RecordHistory(displayPath);

                RebuildSource();
                RecomputeTotals();
            }
    }

    private void RecordHistory(string path)
    {
        NavigationHistory.Remove(path);
        NavigationHistory.Insert(0, path);

        while (NavigationHistory.Count > MaxHistoryEntries)
            NavigationHistory.RemoveAt(NavigationHistory.Count - 1);
    }

    private static IReadOnlyList<IFileSystemItem> Sort(IReadOnlyList<IFileSystemItem> items) =>
        items
            .OrderByDescending(i => i.ItemType == FileSystemItemType.Directory)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<BrowserItem> Sort(IReadOnlyList<BrowserItem> items) =>
        items
            .OrderByDescending(i => i.Item.ItemType == FileSystemItemType.Directory)
            .ThenBy(i => i.Item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void RebuildSource()
    {
        (Source as IDisposable)?.Dispose();
        FilterText = string.Empty;
        IsFilterActive = false;
        // ShowHiddenFiles deliberately isn't reset here - see its own doc comment.
        _rows = BuildRows(_browserItems);
        UpdateRowVisibilityFlags();

        // Built directly from the already-filtered rows, not the full _rows followed by an
        // ApplyVisibility()-style Items swap: TreeSelectionModelBase's own batch-update tracking
        // doesn't tolerate reassigning Items again immediately after a brand-new selection model's
        // own construction (confirmed via a real "No batch update in progress" exception this
        // shape triggered) - a freshly-constructed Source needs to already contain the right rows.
        var visibleRows = _rows.Where(r => r.IsVisible).ToList();
        if (ViewMode == ItemBrowserViewMode.List)
            Source = BuildFlatSource(visibleRows);
        else
            Source = BuildHierarchicalSource(visibleRows);

        // A fresh listing defaults its current item to the first row rather than leaving nothing
        // selected, matching Total Commander. GoBackToParentFolder's NavigateBackToParentAsync
        // overrides this with a more specific choice (the folder just left) right after this
        // returns; every other navigation path keeps this default.
        if (Source.Rows.Count > 0)
            SetCurrentRow(0);

        RecomputeSelection();
        RequestFocus();
    }

    // The row-visibility half of ApplyVisibility, without the Source.Items swap - RebuildSource
    // needs this to decide what to construct a brand-new Source *with*, before one exists to swap
    // Items on (see RebuildSource's own comment on why swapping immediately after construction
    // isn't safe).
    private void UpdateRowVisibilityFlags()
    {
        foreach (var row in _rows)
        {
            row.IsVisible = (ShowHiddenFiles || !row.Item.IsHidden) && QuickFilter.Matches(FilterText, row.Item.Name);
            if (!row.IsVisible)
                row.IsMarked = false;
        }
    }

    private FlatTreeDataGridSource<FileItemRow> BuildFlatSource(IReadOnlyList<FileItemRow> rows)
    {
        var source = new FlatTreeDataGridSource<FileItemRow>(rows)
        {
            Columns =
            {
                CreateNameColumn(),
                new TextColumn<FileItemRow, string>("Ext", x => x.Item.Extension, new GridLength(80)),
                new TextColumn<FileItemRow, string>("Size", x => x.Item.DisplaySize, new GridLength(100)),
                new TextColumn<FileItemRow, DateTime>("Modified", x => x.Item.Modified, new GridLength(150)),
            },
        };
        source.RowSelection!.SingleSelect = false;
        return source;
    }

    private HierarchicalTreeDataGridSource<FileItemRow> BuildHierarchicalSource(IReadOnlyList<FileItemRow> rows)
    {
        var nameColumn = new HierarchicalExpanderColumn<FileItemRow>(
            CreateNameColumn(),
            ChildSelector,
            hasChildrenSelector: x => x.Item.ItemType == FileSystemItemType.Directory);

        var source = new HierarchicalTreeDataGridSource<FileItemRow>(rows)
        {
            Columns =
            {
                nameColumn,
                new TextColumn<FileItemRow, string>("Ext", x => x.Item.Extension, new GridLength(80)),
                new TextColumn<FileItemRow, string>("Size", x => x.Item.DisplaySize, new GridLength(100)),
                new TextColumn<FileItemRow, DateTime>("Modified", x => x.Item.Modified, new GridLength(150)),
            },
        };
        source.RowSelection!.SingleSelect = false;
        return source;
    }

    /// <summary>
    /// Recomputes which of _rows are currently visible - both from the quick filter (FilterText,
    /// see QuickFilter) and from ShowHiddenFiles - and pushes the result into Source.Items. The
    /// single place row visibility is ever decided: SetFilterText, ToggleHiddenFiles, and
    /// RebuildSource all just update their own piece of state and call this, rather than each
    /// maintaining its own "what's visible" logic that the others would need to stay in sync with.
    ///
    /// Swaps Items on the existing source rather than RebuildSource's tear-down-and-recreate
    /// approach, since that would also reconstruct every FileItemRow and re-trigger its async icon
    /// load for what needs to be an instant per-keystroke (or per-toggle) update.
    ///
    /// Swapping Items resets the selection model's source (TreeDataGridRowSelectionModel drops
    /// any current-row cursor no longer present in the new set) - that's cursor-only, though;
    /// a row newly filtered out is also explicitly unmarked here, since IsMarked is tracked
    /// independently of the grid's selection model and swapping Items alone wouldn't touch it -
    /// this is what actually keeps "marked" a subset of "visible" (see FileItemRow.IsMarked).
    /// </summary>
    private void ApplyVisibility()
    {
        // Source.Items replacement resets TreeDataGrid's current-row cursor. Preserve the row
        // object while it remains visible: notably, Escape clearing a quick filter should keep
        // the matching item under the cursor instead of jumping to the full list's first row.
        var currentRow = SelectionModel?.SelectedItem;
        UpdateRowVisibilityFlags();

        if (Source is null)
            return;

        var visibleRows = _rows.Where(r => r.IsVisible).ToList();
        Source.Items = visibleRows;

        if (Source.Rows.Count > 0)
        {
            var retainedIndex = currentRow is null ? -1 : visibleRows.IndexOf(currentRow);
            SetCurrentRow(retainedIndex >= 0 ? retainedIndex : 0);
        }

        RecomputeSelection();

        // Swapping Items recycles TreeDataGrid's row/cell containers, which can leave real
        // keyboard focus sitting on a now-disposed cell instead of following the still-current
        // row - re-request it defensively so a fast burst of filter keystrokes never drops out of
        // the grid mid-typing.
        RequestFocus();
    }

    private void SetFilterText(string text)
    {
        FilterText = text;
        IsFilterActive = FilterText.Length > 0;
        ApplyVisibility();
    }

    /// <summary>
    /// Cmd/Ctrl+. - toggles whether dotfiles/OS-hidden entries are included in the listing (see
    /// ShowHiddenFiles's own doc comment).
    /// </summary>
    private void ToggleHiddenFiles()
    {
        ShowHiddenFiles = !ShowHiddenFiles;
        ApplyVisibility();
    }

    /// <summary>
    /// Appends typed characters to the quick filter - called by the View when printable text is
    /// typed while FileGrid has focus, intercepted before TreeDataGrid's own built-in
    /// type-ahead-jump TextInput handling can see it (see ItemBrowser.axaml.cs).
    /// </summary>
    public void AppendFilterText(string text) => SetFilterText(FilterText + text);

    /// <summary>
    /// Backspace while the filter is active - called by the View instead of letting Backspace
    /// fall through to (no-op) default grid handling.
    /// </summary>
    public void RemoveLastFilterCharacter()
    {
        if (FilterText.Length == 0)
            return;

        SetFilterText(FilterText[..^1]);
    }

    /// <summary>
    /// Escape while the filter is active - called by the View to close the filter bar and show
    /// the folder's full contents again.
    /// </summary>
    public void ClearFilter()
    {
        if (FilterText.Length == 0)
            return;

        SetFilterText(string.Empty);
    }

    // Synchronous bridge over the async provider - acceptable for local I/O (see design notes),
    // but tree-list mode needs real async lazy-loading before a remote provider can use it.
    private IEnumerable<FileItemRow>? ChildSelector(FileItemRow row)
    {
        var provider = row.BrowserItem.Resource.Provider;
        if (!row.BrowserItem.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren))
            return null;

        try
        {
            var children = provider.ListChildrenAsync(row.BrowserItem.Resource.Path).GetAwaiter().GetResult();
            if (!ShowHiddenFiles)
                children = children.Where(i => !i.IsHidden).ToList();

            var container = row.BrowserItem.Resource;
            var browserItems = Sort(children).Select(item => new BrowserItem(
                item,
                new ResourceRef(provider, item.FullPath),
                container,
                provider.ResourceCapabilities));
            return BuildRows(browserItems);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to expand {0}", row.Item.FullPath);
            return null;
        }
    }

    private List<FileItemRow> BuildRows(IEnumerable<BrowserItem> items) =>
        items.Select(i => new FileItemRow(i, _iconCache)).ToList();

    private static readonly IValueConverter NotConverter = new FuncValueConverter<bool, bool>(b => !b);

    // Marked-row coloring (background and text) is handled entirely in ItemBrowser.axaml via
    // style selectors bound to FileItemRow.IsMarked, not here - TreeDataGridRow's DataContext is
    // the FileItemRow model, and covers every column uniformly (not just this one), unlike a
    // per-column Foreground binding would.
    //
    // An instance method (not static, unlike before) so the edit box's Enter/Escape/LostFocus
    // handlers below can close over `this` and call CommitRename/CancelRename directly - F2's
    // in-place editing lives here rather than in a dialog.
    private TemplateColumn<FileItemRow> CreateNameColumn()
    {
        var template = new FuncDataTemplate<FileItemRow>((row, _) =>
        {
            // Grid, not the old StackPanel: the edit TextBox (and the TextBlock it swaps with)
            // need to stretch to fill the Star-width Name column, not size to content.
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };

            var image = new Image
            {
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            image.Bind(Image.SourceProperty, new Binding(nameof(FileItemRow.Icon)));
            Grid.SetColumn(image, 0);

            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            text.Bind(TextBlock.TextProperty, new Binding($"{nameof(FileItemRow.Item)}.{nameof(IFileSystemItem.Name)}"));
            text.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(FileItemRow.IsEditingName)) { Converter = NotConverter });
            Grid.SetColumn(text, 1);

            var editBox = new TextBox { VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(2, 0) };
            editBox.Bind(TextBox.TextProperty, new Binding(nameof(FileItemRow.EditedName)) { Mode = BindingMode.TwoWay });
            editBox.Bind(TextBox.IsVisibleProperty, new Binding(nameof(FileItemRow.IsEditingName)));
            Grid.SetColumn(editBox, 1);

            // This cell/control is recycled across many different FileItemRow models as the grid
            // scrolls - IsVisibleProperty only actually flips true the moment BeginRenameCurrentItem
            // sets IsEditingName on whichever row currently backs it, so reading DataContext here
            // (rather than closing over `row` above) always gets the right one. Deferred a
            // dispatcher tick for the same reason FocusGrid in ItemBrowser.axaml.cs is: focusing
            // synchronously, before layout has actually realized the now-visible TextBox, is
            // unreliable.
            editBox.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.IsVisibleProperty && editBox.IsVisible && editBox.DataContext is FileItemRow currentRow)
                    Dispatcher.UIThread.Post(() => FocusAndSelectBaseName(editBox, currentRow));
            };

            // Enter/Escape are deliberately NOT handled with a KeyDown subscription on editBox
            // itself - FileGrid's own built-in Tunnel-phase key handling would consume them first
            // (Tunnel runs root-to-leaf; editBox is a leaf), so a Bubble-phase handler here would
            // never even see them. See ItemBrowserViewModel.CommitActiveRename/CancelActiveRename
            // and ItemBrowser.axaml.cs's OnPreviewKeyDown, which intercepts Tunnel-phase, above
            // FileGrid, the same way it already does for the quick filter's Backspace/Escape.

            // Clicking away from an in-progress edit commits it (Explorer/TC convention) - this
            // also fires as a side effect of CommitRename/CancelRename themselves hiding the box,
            // but both are idempotent (see their own IsEditingName guard), so that's harmless.
            editBox.LostFocus += (_, _) =>
            {
                if (editBox.DataContext is FileItemRow currentRow)
                    CommitRename(currentRow);
            };

            grid.Children.Add(image);
            grid.Children.Add(text);
            grid.Children.Add(editBox);
            return grid;
        });

        return new TemplateColumn<FileItemRow>("Name", template, width: GridLength.Star);
    }

    // Explorer/TC convention: only the base filename is preselected for a file (so typing
    // immediately replaces the name but leaves the extension), while a directory's whole name is
    // selected (it has no extension to protect).
    private static void FocusAndSelectBaseName(TextBox box, FileItemRow row)
    {
        box.Focus();

        var name = row.Item.Name;
        var selectionEnd = name.Length;

        if (row.Item.ItemType == FileSystemItemType.File)
        {
            var dot = name.LastIndexOf('.');
            if (dot > 0)
                selectionEnd = dot;
        }

        box.SelectionStart = 0;
        box.SelectionEnd = selectionEnd;
    }

    /// <summary>
    /// F2 - starts in-place editing of the cursor row's name (see CreateNameColumn's edit
    /// TextBox). No-op if something's already being edited.
    /// </summary>
    private void BeginRenameCurrentItem()
    {
        if (SelectionModel?.SelectedItem is not { } row || row.IsEditingName ||
            !row.BrowserItem.Capabilities.HasFlag(ResourceCapabilities.Rename))
            return;

        row.EditedName = row.Item.Name;
        row.IsEditingName = true;
    }

    /// <summary>
    /// Commits the in-place edit box's current text as a real rename via the active provider, then
    /// refreshes the listing and reselects the (possibly moved-in-sort-order) renamed item. A
    /// no-op edit (empty, or unchanged) just closes the box without touching the file system.
    /// Guarded by IsEditingName so this is safe to call more than once for the same row (Enter,
    /// then the LostFocus this itself triggers by hiding the box).
    /// </summary>
    private void CommitRename(FileItemRow row)
    {
        if (!row.IsEditingName)
            return;

        row.IsEditingName = false;
        var newName = row.EditedName.Trim();

        if (string.IsNullOrEmpty(newName) || newName == row.Item.Name ||
            !row.BrowserItem.Capabilities.HasFlag(ResourceCapabilities.Rename))
        {
            RequestFocus();
            return;
        }

        _ = ApplyRenameAsync(row.BrowserItem.Resource, newName);
    }

    private async Task ApplyRenameAsync(ResourceRef resource, string newName)
    {
        try
        {
            var newPath = await resource.Provider.RenameAsync(resource.Path, newName);
            await ReloadCurrentListingAsync();
            SelectItemByPath(newPath);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to rename {0} to {1}", resource.Path, newName);
            RequestFocus();
        }
    }

    /// <summary>
    /// Escape while editing - discards the edit box's text and returns to showing the real name,
    /// same idempotency guard as CommitRename.
    /// </summary>
    private void CancelRename(FileItemRow row)
    {
        if (!row.IsEditingName)
            return;

        row.IsEditingName = false;
        RequestFocus();
    }

    /// <summary>
    /// Enter while F2's in-place edit box has focus - called from ItemBrowser.axaml.cs's
    /// Tunnel-phase OnPreviewKeyDown, not the edit box's own Bubble-phase KeyDown, because
    /// FileGrid's own built-in Tunnel-phase key handling (arrow-key/type-ahead navigation)
    /// otherwise consumes Enter first: Tunnel dispatch runs root-to-leaf and marking it Handled
    /// there stops the Bubble phase (leaf-to-root) from ever starting, so the edit box's own
    /// handler never even runs. Same reasoning as the quick filter's Backspace/Escape handling one
    /// level up in that file. A no-op if nothing is currently being edited.
    /// </summary>
    public void CommitActiveRename()
    {
        if (SelectionModel?.SelectedItem is { IsEditingName: true } row)
            CommitRename(row);
    }

    /// <summary>
    /// Escape's counterpart to CommitActiveRename - see its doc comment.
    /// </summary>
    public void CancelActiveRename()
    {
        if (SelectionModel?.SelectedItem is { IsEditingName: true } row)
            CancelRename(row);
    }

    /// <summary>
    /// F7 (via MainWindowViewModel.OpenCreateDirectoryDialog/NewFolderViewModel, which collects
    /// the name) - creates a new subdirectory directly under CurrentPath, then refreshes the
    /// listing and selects it.
    /// </summary>
    public async Task CreateDirectoryAsync(string name)
    {
        if (Context?.WritableDestination is not { } destination ||
            !destination.Capabilities.HasFlag(ContainerCapabilities.CreateDirectory))
            return;

        try
        {
            var newPath = await destination.Resource.Provider.CreateDirectoryAsync(destination.Resource.Path, name);
            await ReloadCurrentListingAsync();
            SelectItemByPath(newPath);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to create directory '{0}' in {1}", name, CurrentPath);
        }
    }

    /// <summary>
    /// Double-click on FileGrid (see ItemBrowser.axaml.cs) - enters the cursor item if the active
    /// provider can (a directory), otherwise hands it to the OS's own default handler (Finder/
    /// Explorer double-click behavior). Deliberately not reused as/merged into
    /// Operation.GoIntoCurrentFolder (Enter/Right): unlike a double-click, Right arrow doubles as
    /// ordinary keyboard navigation and must never launch an external app on a file.
    /// </summary>
    public void OpenOrEnterCurrentItem()
    {
        if (SelectionModel?.SelectedItem is not { } row)
            return;

        var provider = row.BrowserItem.Resource.Provider;
        if (row.BrowserItem.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren))
            _ = NavigateToAsync(row.BrowserItem.Resource);
        else
            _ = OpenExternallyAsync(row.BrowserItem.Resource);
    }

    private async Task OpenExternallyAsync(ResourceRef resource)
    {
        try
        {
            await resource.Provider.OpenExternallyAsync(resource.Path);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to open {0} externally", resource.Path);
        }
    }

    private void RecomputeTotals()
    {
        TotalFileCount = _allItems.Count(i => i.ItemType == FileSystemItemType.File);
        TotalFolderCount = _allItems.Count(i => i.ItemType == FileSystemItemType.Directory);
        TotalSize = _allItems.Where(i => i.ItemType == FileSystemItemType.File).Sum(i => i.Size);
        RecomputeSelection();
    }

    // Driven by IsMarked (Space), not the grid's own SelectionModel - the cursor moving around
    // the grid no longer changes what's counted here, only marking/unmarking does.
    private void RecomputeSelection()
    {
        var marked = _rows.Where(r => r.IsMarked).Select(r => r.Item).ToList();

        SelectedFileCount = marked.Count(i => i.ItemType == FileSystemItemType.File);
        SelectedFolderCount = marked.Count(i => i.ItemType == FileSystemItemType.Directory);
        SelectedSize = marked.Where(i => i.ItemType == FileSystemItemType.File).Sum(i => i.Size);
    }

    /// <summary>
    /// Space - toggles the marked state of whatever row is currently under the cursor (see
    /// FileItemRow.IsMarked). Called by the View only when the quick filter isn't active; while
    /// typing a filter, Space is a word separator instead (ItemBrowser.axaml.cs's
    /// OnPreviewTextInput picks between the two).
    /// </summary>
    public void ToggleMarkCurrentItem()
    {
        if (SelectionModel?.SelectedItem is not { } current)
            return;

        current.IsMarked = !current.IsMarked;
        RecomputeSelection();
    }

    // Alt+R - flips every currently visible row's marked state. Scoped to IsVisible rows only
    // (not all of _rows): rows hidden by the quick filter are never marked (see ApplyVisibility), and
    // reversing them too would mark rows the user can't currently see, breaking that invariant.
    private void ReverseSelection()
    {
        foreach (var row in _rows.Where(r => r.IsVisible))
            row.IsMarked = !row.IsMarked;

        RecomputeSelection();
    }

    /// <summary>
    /// What Copy/Move/Delete operate on: every marked row if any are marked, otherwise whatever's
    /// under the cursor right now - Total Commander's own fallback, so a single-item operation
    /// never requires marking first.
    /// </summary>
    public IReadOnlyList<IFileSystemItem> GetOperationTargets()
        => GetOperationBrowserItems().Select(x => x.Item).ToList();

    public IReadOnlyList<BrowserItem> GetOperationBrowserItems()
    {
        var marked = _rows.Where(r => r.IsMarked).Select(r => r.BrowserItem).ToList();
        if (marked.Count > 0)
            return marked;

        return SelectionModel?.SelectedItem is { } current
            ? new[] { current.BrowserItem }
            : Array.Empty<BrowserItem>();
    }

    private TreeDataGridRowSelectionModel<FileItemRow>? SelectionModel =>
        Source?.Selection as TreeDataGridRowSelectionModel<FileItemRow>;

    private void ToggleViewMode()
    {
        ViewMode = ViewMode == ItemBrowserViewMode.List ? ItemBrowserViewMode.TreeList : ItemBrowserViewMode.List;
        RebuildSource();
    }

    private void ExpandCurrentFolder()
    {
        if (Context?.Location is not { } location)
            return;

        _ = NavigateToListingAsync(
            new ExpandedListingSource([location]),
            $"Branch: {location.Path}");
    }

    private void ExpandSelectedFolders()
    {
        var roots = _rows
            .Where(row => row.IsMarked && row.BrowserItem.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren))
            .Select(row => row.BrowserItem.Resource)
            .ToList();

        if (roots.Count == 0 && SelectionModel?.SelectedItem is { } current &&
            current.BrowserItem.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren))
        {
            roots.Add(current.BrowserItem.Resource);
        }

        if (roots.Count == 0)
            return;

        _ = NavigateToListingAsync(
            new ExpandedListingSource(roots),
            roots.Count == 1 ? $"Branch: {roots[0].Path}" : $"Branch: {roots.Count} folders");
    }

    private void GoIntoCurrentFolder()
    {
        var resource = GetSelectedEnterableResource();
        if (resource is not null)
            _ = NavigateToAsync(resource.Value);
    }

    /// <summary>
    /// The currently-selected item's path, if it's a single directory the active provider can
    /// enter - otherwise null. Used by MainPanelViewModel's OpenSelectedFolderInNewTab, which
    /// needs this same "what folder is selected right now" check but, unlike GoIntoCurrentFolder,
    /// acts on a sibling tab rather than this one.
    /// </summary>
    public string? GetSelectedEnterablePath()
        => GetSelectedEnterableResource()?.Path;

    public ResourceRef? GetSelectedEnterableResource()
    {
        var selected = SelectionModel?.SelectedItem;
        return selected is not null &&
               selected.BrowserItem.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren)
            ? selected.BrowserItem.Resource
            : null;
    }

    /// <summary>
    /// Ctrl/Cmd+R - re-reads CurrentPath from the file system without navigating away (picking up
    /// changes made outside CatCommander - another app writing a file, a mounted drive's contents
    /// changing, ...), restoring the cursor to the same item afterward if it's still there. Same
    /// "capture a path, re-navigate, restore" shape CreateDirectoryAsync/ApplyRenameAsync already
    /// use after their own listing-changing operations - refresh is just that same pattern with no
    /// operation of its own in between.
    /// </summary>
    private void RefreshCurrentFolder()
    {
        var selectedPath = SelectionModel?.SelectedItem?.Item.FullPath;
        _ = RefreshCurrentFolderAsync(selectedPath);
    }

    private async Task RefreshCurrentFolderAsync(string? previouslySelectedPath)
    {
        await ReloadCurrentListingAsync();
        if (previouslySelectedPath is not null)
            SelectItemByPath(previouslySelectedPath);
    }

    private Task ReloadCurrentListingAsync() => Context?.Listing is { } listing
        ? NavigateToListingAsync(listing, CurrentPath)
        : Task.CompletedTask;

    private void GoBackToParentFolder()
    {
        var current = SelectionModel?.SelectedItem?.BrowserItem;
        var target = Context?.GetBackTarget(current);
        if (target is not null)
            _ = NavigateBackToParentAsync(target.Value, current?.Resource);
    }

    // Keeps the folder just left selected in the parent listing, so browsing down a tree of
    // subfolders one at a time is Right, Left, Down, Right, Left, Down, ... instead of Left
    // dropping the cursor back to the top of the list every time.
    private async Task NavigateBackToParentAsync(ResourceRef parent, ResourceRef? child)
    {
        await NavigateToAsync(parent);
        if (child is not null)
            SelectItemByPath(child.Value.Path);
    }

    private void SelectItemByPath(string path)
    {
        var itemIndex = -1;
        for (var i = 0; i < _allItems.Count; i++)
        {
            if (string.Equals(_allItems[i].FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                itemIndex = i;
                break;
            }
        }

        if (itemIndex < 0)
            return;

        SetCurrentRow(itemIndex);
    }

    private void GotoFirstItem()
    {
        if (Source?.Rows.Count > 0)
            SetCurrentRow(0);
    }

    private void GotoLastItem()
    {
        var count = Source?.Rows.Count ?? 0;
        if (count > 0)
            SetCurrentRow(count - 1);
    }

    public ICommand? GetCommand(Operation operation)
    {
        var current = SelectionModel?.SelectedItem?.BrowserItem;
        var available = operation switch
        {
            Operation.Rename => current?.Capabilities.HasFlag(ResourceCapabilities.Rename) == true,
            Operation.ExpandCurrentFolder => Context?.Location is not null,
            Operation.ExpandSelectedFolders => _rows.Any(row =>
                (row.IsMarked || ReferenceEquals(row.BrowserItem, current)) &&
                row.BrowserItem.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren)),
            Operation.GoBackToParentFolder => Context?.GetBackTarget(current) is not null,
            Operation.OpenTerminal => _terminalLauncher is not null && GetLocalShellDirectory() is not null,
            _ => true,
        };

        return available ? _commands.GetValueOrDefault(operation) : null;
    }
}
