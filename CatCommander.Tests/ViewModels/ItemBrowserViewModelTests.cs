using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Selection;
using CatCommander.Browsing;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Services;
using CatCommander.ViewModels;
using Xunit;
using System.IO.Compression;

namespace CatCommander.Tests.ViewModels;

public class ItemBrowserViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _child;

    public ItemBrowserViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderIBVMTests_" + Guid.NewGuid().ToString("N"));
        _child = Path.Combine(_root, "child");
        Directory.CreateDirectory(_child);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static ItemBrowserViewModel CreateViewModel()
    {
        var registry = new FileSystemProviderRegistry();
        registry.Register(new LocalFileSystemProviderFactory());
        return new ItemBrowserViewModel(registry, new IconCache());
    }

    [Fact]
    public async Task ExpandCurrentFolder_BuildsReadOnlyBranchListing_WithOriginalContainers()
    {
        var nestedFile = Path.Combine(_child, "nested.txt");
        File.WriteAllText(nestedFile, "content");
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);

        vm.GetCommand(Operation.ExpandCurrentFolder)!.Execute(null);
        for (var i = 0; i < 100 && vm.Context?.Kind != ListingKind.ExpandedResults; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.Equal(ListingKind.ExpandedResults, vm.Context?.Kind);
        Assert.Equal(ItemBrowserViewMode.List, vm.ViewMode);
        Assert.Null(vm.WritableDestination);
        var nested = Assert.Single(
            vm.Source!.Items.Cast<FileItemRow>(),
            row => row.Item.FullPath == nestedFile);
        Assert.Equal(_child, nested.BrowserItem.Container?.Path);
        Assert.Equal("nested.txt", nested.Item.Name);
        Assert.Equal(nestedFile, nested.BrowserItem.Resource.Path);
        Assert.Equal(new Avalonia.Thickness(0), nested.BranchIndent);

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal(ItemBrowserViewMode.List, vm.ViewMode);
    }

    [Fact]
    public async Task ExpandSelectedFolders_ExpandsEveryMarkedFolder_AndNoUnselectedSibling()
    {
        var other = Directory.CreateDirectory(Path.Combine(_root, "other")).FullName;
        var childFile = Path.Combine(_child, "child.txt");
        var otherFile = Path.Combine(other, "other.txt");
        var unselectedFile = Path.Combine(_root, "root.txt");
        File.WriteAllText(childFile, "child");
        File.WriteAllText(otherFile, "other");
        File.WriteAllText(unselectedFile, "root");
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);

        vm.ToggleMarkCurrentItem();
        ((TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!).SelectedIndex =
            new Avalonia.Controls.IndexPath(1);
        vm.ToggleMarkCurrentItem();
        vm.GetCommand(Operation.ExpandSelectedFolders)!.Execute(null);
        await WaitUntilAsync(() => vm.Context?.Kind == ListingKind.ExpandedResults);

        var paths = vm.Source!.Items.Cast<FileItemRow>().Select(row => row.Item.FullPath).ToList();
        Assert.Contains(childFile, paths);
        Assert.Contains(otherFile, paths);
        Assert.DoesNotContain(unselectedFile, paths);
        Assert.Null(vm.WritableDestination);
    }

    [Fact]
    public async Task LeftFromBranchListing_NavigatesToCurrentItemsContainer()
    {
        var nestedFile = Path.Combine(_child, "nested.txt");
        File.WriteAllText(nestedFile, "content");
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_child);
        vm.GetCommand(Operation.ExpandCurrentFolder)!.Execute(null);

        for (var i = 0; i < 100 && vm.Context?.Kind != ListingKind.ExpandedResults; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        vm.GetCommand(Operation.GoBackToParentFolder)!.Execute(null);
        for (var i = 0; i < 100 && vm.Context?.Kind != ListingKind.Directory; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.Equal(_child, vm.CurrentPath);
        Assert.Equal(ListingKind.Directory, vm.Context?.Kind);
    }

    [Fact]
    public async Task NavigateToAsync_DefaultsCurrentItem_ToFirstRow()
    {
        // Total Commander always has a current item after navigating, even with nothing marked -
        // defaults to row 0.
        var grandchild = Path.Combine(_child, "grandchild");
        Directory.CreateDirectory(grandchild);

        var vm = CreateViewModel();
        await vm.NavigateToAsync(_child);

        var selection = (TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!;
        Assert.Equal(grandchild, selection.SelectedItem?.Item.FullPath);
    }

    [Fact]
    public async Task CreateDirectoryAsync_MakesTheNewDirectoryCurrent_AfterSortedReload()
    {
        Directory.CreateDirectory(Path.Combine(_root, "alpha"));
        Directory.CreateDirectory(Path.Combine(_root, "zeta"));
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);

        // Keep a non-provider ordering active across the reload. The old implementation looked
        // up the new path in _allItems, then incorrectly used that index in the sorted grid.
        vm.GetCommand(Operation.SortByName)!.Execute(null);
        vm.GetCommand(Operation.SortByName)!.Execute(null);

        await vm.CreateDirectoryAsync("middle");

        var selection = Assert.IsType<TreeDataGridRowSelectionModel<FileItemRow>>(vm.Source!.Selection);
        Assert.Equal(Path.Combine(_root, "middle"), selection.SelectedItem?.Item.FullPath);
    }

    [Fact]
    public async Task GoBackToParentFolder_SelectsTheFolderJustLeft()
    {
        // Lets browsing down a tree of subfolders one at a time be Right, Left, Down, Right,
        // Left, Down, ... instead of Left dropping the cursor back to the top of the list.
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_child);

        vm.GetCommand(Operation.GoBackToParentFolder)!.Execute(null);

        // GoBackToParentFolder kicks off navigation-then-select fire-and-forget (it has to - it's
        // a synchronous ICommand). Polling on CurrentPath alone races selection: CurrentPath is
        // set partway through NavigateToAsync, before the *select* half of the flow even starts.
        // Polling on "any selection" alone now also races: RebuildSource itself defaults the
        // selection to row 0 as soon as navigation lands, *before* GoBackToParentFolder's own
        // more specific selection (the folder just left) gets a chance to run - so "any selection"
        // can be satisfied a step too early. Wait for the actual target selection, not an earlier
        // intermediate one.
        TreeDataGridRowSelectionModel<FileItemRow> selection = (TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!;
        for (var i = 0; i < 100 && selection.SelectedItem?.Item.FullPath != _child; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            selection = (TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!;
        }

        Assert.Equal(_root, vm.CurrentPath);
        Assert.Equal(_child, selection.SelectedItem?.Item.FullPath);
    }

    [Fact]
    public async Task GoBackToParentFolder_SelectsDirectoryJustLeft_NotItsCurrentChild()
    {
        var grandchild = Directory.CreateDirectory(Path.Combine(_child, "grandchild")).FullName;
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_child);
        Assert.Equal(grandchild,
            ((TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!).SelectedItem?.Item.FullPath);

        vm.GetCommand(Operation.GoBackToParentFolder)!.Execute(null);
        await WaitUntilAsync(() =>
            ((TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!).SelectedItem?.Item.FullPath == _child);

        Assert.Equal(_root, vm.CurrentPath);
        Assert.Equal(_child,
            ((TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!).SelectedItem?.Item.FullPath);
    }

    [Fact]
    public async Task NavigateToAsync_RecordsHistory_MostRecentFirst_Deduplicated()
    {
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        await vm.NavigateToAsync(_child);
        await vm.NavigateToAsync(_root); // revisiting should move it back to front, not duplicate it

        Assert.Equal(new[] { _root, _child }, vm.NavigationHistory);
    }

    [Fact]
    public async Task HistoryBackAndForward_NavigatePerTabAndClearForwardOnNewNavigation()
    {
        var grandchild = Directory.CreateDirectory(Path.Combine(_child, "grandchild")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(_root, "other")).FullName;
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        await vm.NavigateToAsync(_child);
        await vm.NavigateToAsync(grandchild);

        vm.GetCommand(Operation.GoBackInHistory)!.Execute(null);
        await WaitUntilAsync(() => vm.CurrentPath == _child);
        vm.GetCommand(Operation.GoBackInHistory)!.Execute(null);
        await WaitUntilAsync(() => vm.CurrentPath == _root);

        vm.GetCommand(Operation.GoForwardInHistory)!.Execute(null);
        await WaitUntilAsync(() => vm.CurrentPath == _child);

        await vm.NavigateToAsync(other);
        Assert.Null(vm.GetCommand(Operation.GoForwardInHistory));
    }

    [Fact]
    public async Task ConcurrentNavigations_CommitOnlyLatestListingWithoutSelectionBatchFailure()
    {
        var other = Path.Combine(_root, "other");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "latest.txt"), "latest");
        var vm = CreateViewModel();

        var navigations = Enumerable.Range(0, 40)
            .Select(i => vm.NavigateToAsync(i == 39 ? other : _root))
            .ToArray();
        await Task.WhenAll(navigations);

        Assert.Equal(other, vm.CurrentPath);
        var selection = (TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!;
        Assert.Equal(Path.Combine(other, "latest.txt"), selection.SelectedItem?.Item.FullPath);
    }

    [Fact]
    public async Task OpenTerminal_IsAvailableForLocalDirectory_AndUsesCurrentDirectory()
    {
        var terminal = new RecordingTerminalLauncher();
        var vm = new ItemBrowserViewModel(_registryForTest(), new IconCache(), terminal);
        await vm.NavigateToAsync(_root);

        var command = vm.GetCommand(Operation.OpenTerminal);
        Assert.NotNull(command);
        command.Execute(null);

        Assert.Equal(_root, terminal.OpenedDirectory);
    }

    [Fact]
    public async Task PreviewAndEdit_UseCurrentLocalFile()
    {
        var file = Path.Combine(_root, "preview.txt");
        File.WriteAllText(file, "content");
        var launcher = new RecordingFileLauncher();
        var vm = new ItemBrowserViewModel(
            _registryForTest(), new IconCache(), fileLauncher: launcher);
        await vm.NavigateToAsync(_root);
        var selection = Assert.IsType<TreeDataGridRowSelectionModel<FileItemRow>>(vm.Source!.Selection);
        selection.SelectedIndex = new Avalonia.Controls.IndexPath(1);

        vm.GetCommand(Operation.PreviewFile)!.Execute(null);
        vm.GetCommand(Operation.EditFile)!.Execute(null);

        Assert.Equal(file, launcher.PreviewedPath);
        Assert.Equal(file, launcher.EditedPath);
    }

    [Fact]
    public async Task RightOnLocalArchive_EntersItsVirtualRoot_AndLeftReturnsToContainingDirectory()
    {
        var archiveDirectory = Directory.CreateDirectory(Path.Combine(_root, "archives")).FullName;
        var archivePath = Path.Combine(archiveDirectory, "sample.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            archive.CreateEntry("inside.txt");
        var passwords = new ProviderCredentialStore();
        var registry = new FileSystemProviderRegistry();
        var localProvider = new LocalFileSystemProvider();
        registry.Register(new ArchiveFileSystemProviderFactory(passwords, localProvider));
        registry.Register(new LocalFileSystemProviderFactory(localProvider));
        var vm = new ItemBrowserViewModel(registry, new IconCache());
        await vm.NavigateToAsync(archiveDirectory);

        vm.GetCommand(Operation.GoIntoCurrentFolder)!.Execute(null);
        await WaitUntilAsync(() =>
            vm.Provider is ArchiveFileSystemProvider &&
            vm.CurrentPath == $"{archivePath}!/" &&
            vm.Source?.Items.Cast<FileItemRow>().SingleOrDefault()?.Item.Name == "inside.txt");

        Assert.IsType<ArchiveFileSystemProvider>(vm.Provider);
        Assert.Equal($"{archivePath}!/", vm.CurrentPath);
        Assert.Equal("inside.txt", Assert.Single(vm.Source!.Items.Cast<FileItemRow>()).Item.Name);

        vm.GetCommand(Operation.GoBackToParentFolder)!.Execute(null);
        await WaitUntilAsync(() => vm.Provider is LocalFileSystemProvider);
        Assert.Equal(archiveDirectory, vm.CurrentPath);
        Assert.Equal(archivePath,
            ((TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!).SelectedItem?.Item.FullPath);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    private FileSystemProviderRegistry _registryForTest()
    {
        var registry = new FileSystemProviderRegistry();
        registry.Register(new LocalFileSystemProviderFactory());
        return registry;
    }

    private sealed class RecordingTerminalLauncher : ITerminalLauncher
    {
        public string? OpenedDirectory { get; private set; }
        public void Open(string directory) => OpenedDirectory = directory;
    }

    private sealed class RecordingFileLauncher : IFileLauncher
    {
        public string? PreviewedPath { get; private set; }
        public string? EditedPath { get; private set; }
        public void Preview(string path) => PreviewedPath = path;
        public void Edit(string path) => EditedPath = path;
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Text { get; private set; }
        public IReadOnlyList<string>? Files { get; private set; }
        public Task SetTextAsync(string text)
        {
            Text = text;
            return Task.CompletedTask;
        }
        public Task SetFilesAsync(IReadOnlyList<string> paths)
        {
            Files = paths;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SelectionCommands_UseMarks_AndNativeCopyUsesFilePaths()
    {
        var file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "a");
        var clipboard = new RecordingClipboard();
        var fileClipboard = new FileClipboardState();
        var vm = new ItemBrowserViewModel(
            _registryForTest(), new IconCache(), clipboard: clipboard, fileClipboard: fileClipboard);
        await vm.NavigateToAsync(_root);
        var gridSelection = Assert.IsType<TreeDataGridRowSelectionModel<FileItemRow>>(vm.Source!.Selection);
        Assert.True(gridSelection.SingleSelect);

        vm.GetCommand(Operation.SelectAll)!.Execute(null);
        Assert.Equal(1, vm.SelectedFolderCount);
        Assert.Equal(1, vm.SelectedFileCount);

        vm.GetCommand(Operation.CopyFilesToClipboard)!.Execute(null);
        Assert.Equal(new[] { _child, file }, clipboard.Files);
        Assert.Equal(2, fileClipboard.Items.Count);
        Assert.False(fileClipboard.MoveOnPaste);

        vm.GetCommand(Operation.CutFilesToClipboard)!.Execute(null);
        Assert.Equal(2, fileClipboard.Items.Count);
        Assert.True(fileClipboard.MoveOnPaste);
        Assert.Same(vm, fileClipboard.SourceTab);

        vm.GetCommand(Operation.ClearSelection)!.Execute(null);
        Assert.Equal(0, vm.SelectedFolderCount);
        Assert.Equal(0, vm.SelectedFileCount);

        vm.AppendFilterText("a");
        Assert.Null(vm.GetCommand(Operation.ClearSelection));
    }

    [Fact]
    public async Task ClipboardOperations_CopyContainer_CurrentItem_AndMarkedItemLists()
    {
        var a = Path.Combine(_root, "a.txt");
        var b = Path.Combine(_root, "b.txt");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        var clipboard = new RecordingClipboard();
        var vm = new ItemBrowserViewModel(_registryForTest(), new IconCache(), clipboard: clipboard);
        await vm.NavigateToAsync(_root);

        vm.GetCommand(Operation.CopyContainerPath)!.Execute(null);
        Assert.Equal(_root, clipboard.Text);

        vm.GetCommand(Operation.CopyItemNames)!.Execute(null);
        Assert.Equal("child", clipboard.Text);

        vm.ToggleMarkCurrentItem();
        ((TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!).SelectedIndex = new Avalonia.Controls.IndexPath(1);
        vm.ToggleMarkCurrentItem();

        vm.GetCommand(Operation.CopyItemNames)!.Execute(null);
        Assert.Equal(string.Join(Environment.NewLine, "child", "a.txt"), clipboard.Text);

        vm.GetCommand(Operation.CopyItemPaths)!.Execute(null);
        Assert.Equal(string.Join(Environment.NewLine, _child, a), clipboard.Text);
    }

    [Fact]
    public async Task SortOperations_ToggleDirection_AndPreserveCurrentItem()
    {
        var smallBin = Path.Combine(_root, "z.bin");
        var largeText = Path.Combine(_root, "a.txt");
        File.WriteAllText(smallBin, "1");
        File.WriteAllText(largeText, "12345");
        File.SetLastWriteTime(smallBin, new DateTime(2020, 1, 1));
        File.SetLastWriteTime(largeText, new DateTime(2021, 1, 1));
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        var selection = (TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!;
        selection.SelectedIndex = new Avalonia.Controls.IndexPath(2); // z.bin

        vm.GetCommand(Operation.SortByName)!.Execute(null);
        Assert.Equal(new[] { "a.txt", "z.bin" }, VisibleFileNames(vm));
        Assert.Equal("▲", SortArrow(vm, 0));
        Assert.Equal(smallBin, selection.SelectedItem?.Item.FullPath);
        vm.GetCommand(Operation.SortByName)!.Execute(null);
        Assert.Equal(new[] { "z.bin", "a.txt" }, VisibleFileNames(vm));
        Assert.Equal("▼", SortArrow(vm, 0));

        vm.GetCommand(Operation.SortByExtension)!.Execute(null);
        Assert.Equal(new[] { "z.bin", "a.txt" }, VisibleFileNames(vm));
        Assert.Equal(string.Empty, SortArrow(vm, 0));
        Assert.Equal("▲", SortArrow(vm, 1));
        vm.GetCommand(Operation.SortByExtension)!.Execute(null);
        Assert.Equal(new[] { "a.txt", "z.bin" }, VisibleFileNames(vm));

        vm.GetCommand(Operation.SortByDate)!.Execute(null);
        Assert.Equal(new[] { "z.bin", "a.txt" }, VisibleFileNames(vm));
        vm.GetCommand(Operation.SortBySize)!.Execute(null);
        Assert.Equal(new[] { "z.bin", "a.txt" }, VisibleFileNames(vm));
    }

    private static string[] VisibleFileNames(ItemBrowserViewModel vm) => vm.Source!.Items
        .Cast<FileItemRow>()
        .Where(row => row.Item.ItemType == FileSystemItemType.File)
        .Select(row => row.Item.Name)
        .ToArray();

    private static string? SortArrow(ItemBrowserViewModel vm, int columnIndex)
    {
        var header = Assert.IsType<Avalonia.Controls.StackPanel>(vm.Source!.Columns[columnIndex].Header);
        return Assert.IsType<Avalonia.Controls.TextBlock>(header.Children[1]).Text;
    }

    [Fact]
    public async Task ToggleViewMode_DoesNotRecordHistory()
    {
        // ToggleViewMode rebuilds the Source (same as any navigation) but never calls
        // NavigateToAsync, so it shouldn't touch the address bar's history at all.
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        vm.ToggleViewModeCommand.Execute(null);

        Assert.Equal(new[] { _root }, vm.NavigationHistory);
    }

    [Fact]
    public async Task NavigateToHistoryEntryCommand_NavigatesToThePath()
    {
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_child);

        vm.NavigateToHistoryEntryCommand.Execute(_root);

        for (var i = 0; i < 100 && vm.CurrentPath != _root; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.Equal(_root, vm.CurrentPath);
    }

    [Fact]
    public async Task ToggleMarkCurrentItem_TogglesTheCursorRow_AndUpdatesSelectionCounts()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "hello");

        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        // Rows: "child" (folder, sorts first), "a.txt". Cursor defaults to row 0 ("child").

        vm.ToggleMarkCurrentItem();
        Assert.Equal(1, vm.SelectedFolderCount);
        Assert.Equal(0, vm.SelectedFileCount);

        vm.GetCommand(Operation.GotoLastItem)!.Execute(null); // "a.txt"
        vm.ToggleMarkCurrentItem();
        Assert.Equal(1, vm.SelectedFolderCount);
        Assert.Equal(1, vm.SelectedFileCount);

        vm.GetCommand(Operation.GotoFirstItem)!.Execute(null); // back to "child"
        vm.ToggleMarkCurrentItem(); // unmark it
        Assert.Equal(0, vm.SelectedFolderCount);
        Assert.Equal(1, vm.SelectedFileCount);
    }

    [Fact]
    public async Task ShiftDown_MarksBothTheCurrentAndDestinationRows_ThenMovesCurrent()
    {
        var fileA = Path.Combine(_root, "a.txt");
        var fileB = Path.Combine(_root, "b.txt");
        File.WriteAllText(fileA, "a");
        File.WriteAllText(fileB, "b");
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        var selection = Assert.IsType<TreeDataGridRowSelectionModel<FileItemRow>>(vm.Source!.Selection);
        Assert.Equal(_child, selection.SelectedItem?.Item.FullPath);

        vm.GetCommand(Operation.SelectAndMoveDown)!.Execute(null);

        Assert.Equal(fileA, selection.SelectedItem?.Item.FullPath);
        Assert.Equal(2, vm.SelectedFolderCount + vm.SelectedFileCount);
        Assert.True(vm.Source.Items.Cast<FileItemRow>().Single(row => row.Item.FullPath == _child).IsMarked);
        Assert.True(vm.Source.Items.Cast<FileItemRow>().Single(row => row.Item.FullPath == fileA).IsMarked);

        vm.GetCommand(Operation.SelectAndMoveDown)!.Execute(null);
        Assert.Equal(fileB, selection.SelectedItem?.Item.FullPath);
        Assert.Equal(3, vm.SelectedFolderCount + vm.SelectedFileCount);

        vm.GetCommand(Operation.SelectAndMoveUp)!.Execute(null);
        Assert.Equal(fileA, selection.SelectedItem?.Item.FullPath);
        Assert.Equal(3, vm.SelectedFolderCount + vm.SelectedFileCount);
    }

    [Fact]
    public async Task NavigateToAsync_ClearsMarks_FromThePreviousListing()
    {
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        vm.ToggleMarkCurrentItem(); // marks "child"
        Assert.Equal(1, vm.SelectedFolderCount);

        await vm.NavigateToAsync(_child);
        Assert.Equal(0, vm.SelectedFolderCount);
        Assert.Equal(0, vm.SelectedFileCount);

        // Not remembered even on a round trip back to the same directory - a fresh listing always
        // starts unmarked.
        await vm.NavigateToAsync(_root);
        Assert.Equal(0, vm.SelectedFolderCount);
    }

    [Fact]
    public async Task ReverseSelection_FlipsMarkedStateOfEveryVisibleRow()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "world");

        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        // Rows: "child" (folder), "a.txt", "b.txt".

        vm.GetCommand(Operation.GotoLastItem)!.Execute(null); // "b.txt"
        vm.ToggleMarkCurrentItem(); // mark only b.txt

        vm.GetCommand(Operation.ReverseSelection)!.Execute(null);

        // child + a.txt now marked, b.txt no longer.
        Assert.Equal(1, vm.SelectedFolderCount);
        Assert.Equal(1, vm.SelectedFileCount);
    }

    [Fact]
    public async Task GetOperationTargets_ReturnsMarkedItems_OrFallsBackToTheCursorRow()
    {
        var fileA = Path.Combine(_root, "a.txt");
        File.WriteAllText(fileA, "hello");

        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);

        // Nothing marked - falls back to whatever's under the cursor ("child", row 0).
        Assert.Equal(new[] { _child }, vm.GetOperationTargets().Select(t => t.FullPath));

        vm.GetCommand(Operation.GotoLastItem)!.Execute(null); // "a.txt"
        vm.ToggleMarkCurrentItem();

        Assert.Equal(new[] { fileA }, vm.GetOperationTargets().Select(t => t.FullPath));
    }

    [Fact]
    public async Task PrepareItemContextMenu_PreservesMarkedGroup_OrMakesUnmarkedRowTheSoleTarget()
    {
        var fileA = Path.Combine(_root, "a.txt");
        var fileB = Path.Combine(_root, "b.txt");
        File.WriteAllText(fileA, "a");
        File.WriteAllText(fileB, "b");
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        var rows = vm.Source!.Items.Cast<FileItemRow>().ToList();
        var child = rows.Single(row => row.Item.FullPath == _child);
        var a = rows.Single(row => row.Item.FullPath == fileA);
        var b = rows.Single(row => row.Item.FullPath == fileB);

        child.IsMarked = true;
        a.IsMarked = true;
        vm.PrepareItemContextMenu(a);

        Assert.Equal(2, vm.GetOperationTargets().Count);
        Assert.Contains(vm.GetOperationTargets(), item => item.FullPath == _child);
        Assert.Contains(vm.GetOperationTargets(), item => item.FullPath == fileA);

        vm.PrepareItemContextMenu(b);

        Assert.False(child.IsMarked);
        Assert.False(a.IsMarked);
        Assert.Equal(new[] { fileB }, vm.GetOperationTargets().Select(item => item.FullPath));
    }

    [Fact]
    public async Task OpenOrEnterCurrentItem_EntersTheDirectory_WhenCursorIsOnOne()
    {
        // Double-click (ItemBrowser.axaml.cs's FileGrid.DoubleTapped) - deliberately not exercised
        // here via a real double-click gesture (Avalonia's DoubleTapped recognizer is a framework
        // feature, not something this app needs to re-verify); this covers the actual custom logic,
        // OpenOrEnterCurrentItem's own CanEnter branch.
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        // Rows: "child" (folder, sorts first) - cursor defaults to row 0.

        vm.OpenOrEnterCurrentItem();

        for (var i = 0; i < 100 && vm.CurrentPath != _child; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.Equal(_child, vm.CurrentPath);
    }

    // OpenOrEnterCurrentItem's other branch (a file, CanEnter false) deliberately has no automated
    // test: it calls _provider.OpenExternallyAsync, which for the real LocalFileSystemProvider is
    // Process.Start - actually invoking it here (or in LocalFileSystemProviderTests) would launch
    // whatever app owns .txt files on the machine running the tests. That branch is a direct
    // three-line call into OpenExternallyAsync, so it's covered by code review rather than a
    // fake-provider seam that isn't worth adding just for this.

    [Fact]
    public async Task AppendFilterText_UnmarksRowsThatBecomeInvisible()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "hello");

        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        vm.ToggleMarkCurrentItem(); // marks "child" (row 0)
        Assert.Equal(1, vm.SelectedFolderCount);

        vm.AppendFilterText("a.txt"); // filters out "child", leaving only "a.txt" visible

        Assert.Equal(0, vm.SelectedFolderCount); // "child" was unmarked when it became invisible
    }

    [Fact]
    public async Task ClearFilter_KeepsCurrentMatchingItem()
    {
        var matchingPath = Path.Combine(_root, "match.txt");
        File.WriteAllText(matchingPath, "match");
        File.WriteAllText(Path.Combine(_root, "other.txt"), "other");
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);

        vm.AppendFilterText("match");
        var filteredSelection = (TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!;
        Assert.Equal(matchingPath, filteredSelection.SelectedItem?.Item.FullPath);

        vm.ClearFilter();
        var restoredSelection = (TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!;
        Assert.Equal(string.Empty, vm.FilterText);
        Assert.Equal(matchingPath, restoredSelection.SelectedItem?.Item.FullPath);
    }

    [Fact]
    public async Task Refresh_PicksUpChangesMadeOutsideTheApp_AndKeepsTheCursorOnTheSameItem()
    {
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        // Rows: "child" (the only entry so far) - cursor defaults to row 0.

        // Simulate another process creating a file while this folder is already open.
        var newFile = Path.Combine(_root, "a.txt");
        File.WriteAllText(newFile, "hello");

        vm.GetCommand(Operation.Refresh)!.Execute(null);

        for (var i = 0; i < 100 && vm.TotalFileCount == 0; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        var selection = (TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!;
        Assert.Equal(1, vm.TotalFileCount); // "a.txt" is now picked up
        Assert.Equal(_child, selection.SelectedItem?.Item.FullPath); // cursor stayed on "child"
    }

    [Fact]
    public async Task Refresh_KeepsCurrentItem_WhenHiddenRowsChangeItsUnderlyingIndex()
    {
        File.WriteAllText(Path.Combine(_root, ".hidden.txt"), "hidden");
        var visiblePath = Path.Combine(_root, "visible.txt");
        File.WriteAllText(visiblePath, "visible");
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);

        var selection = Assert.IsType<TreeDataGridRowSelectionModel<FileItemRow>>(vm.Source!.Selection);
        selection.SelectedIndex = new Avalonia.Controls.IndexPath(1); // child, visible.txt
        Assert.Equal(visiblePath, selection.SelectedItem?.Item.FullPath);

        vm.GetCommand(Operation.Refresh)!.Execute(null);
        await WaitUntilAsync(() =>
            ((TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!).SelectedItem?.Item.FullPath == visiblePath);

        Assert.Equal(visiblePath,
            ((TreeDataGridRowSelectionModel<FileItemRow>)vm.Source!.Selection!).SelectedItem?.Item.FullPath);
    }

    [Fact]
    public async Task FileOperationRefresh_PreservesFilterUntilEscapeClearsIt()
    {
        File.WriteAllText(Path.Combine(_root, "match.txt"), "match");
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        vm.AppendFilterText("match");

        File.WriteAllText(Path.Combine(_root, "match-new.txt"), "new");
        await vm.RefreshListingAfterFileOperationAsync();

        Assert.True(vm.IsFilterActive);
        Assert.Equal("match", vm.FilterText);
        Assert.All(vm.Source!.Items.Cast<FileItemRow>(), row =>
            Assert.Contains("match", row.Item.Name, StringComparison.OrdinalIgnoreCase));

        vm.ClearFilter();
        Assert.False(vm.IsFilterActive);
        Assert.Equal(string.Empty, vm.FilterText);
    }

    [Fact]
    public async Task ToggleHiddenFiles_HidesDotfilesByDefault_AndShowsThemWhenToggled()
    {
        File.WriteAllText(Path.Combine(_root, ".hidden.txt"), "secret");
        File.WriteAllText(Path.Combine(_root, "visible.txt"), "public");

        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        // _allItems: "child" (dir), ".hidden.txt", "visible.txt" - 3 real entries.

        Assert.False(vm.ShowHiddenFiles);
        Assert.Equal(2, vm.Source!.Rows.Count); // ".hidden.txt" excluded by default

        vm.GetCommand(Operation.ToggleHiddenFiles)!.Execute(null);

        Assert.True(vm.ShowHiddenFiles);
        Assert.Equal(3, vm.Source!.Rows.Count); // now included
    }

    [Fact]
    public async Task ToggleHiddenFiles_PersistsAcrossNavigation_UnlikeTheQuickFilter()
    {
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        vm.GetCommand(Operation.ToggleHiddenFiles)!.Execute(null);
        Assert.True(vm.ShowHiddenFiles);

        await vm.NavigateToAsync(_child);

        Assert.True(vm.ShowHiddenFiles); // not reset by RebuildSource, unlike FilterText
    }

    [Fact]
    public async Task ProviderWithoutAsyncTreeSupport_CannotEnterSynchronousTreeMode()
    {
        var provider = new RemoteStyleProvider();
        var vm = new ItemBrowserViewModel(new FileSystemProviderRegistry(), new IconCache());
        await vm.NavigateToAsync(new CatCommander.Resources.ResourceRef(provider, "/"));

        vm.ToggleViewModeCommand.Execute(null);

        Assert.Equal(ItemBrowserViewMode.List, vm.ViewMode);
    }

    private sealed class RemoteStyleProvider : IFileSystemProvider
    {
        public string Id => "remote:test";
        public bool TracksHistory => true;
        public bool CanEnter(IFileSystemItem item) => item.ItemType == FileSystemItemType.Directory;
        public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream());
        public Task<IReadOnlyList<IFileSystemItem>> ListChildrenAsync(string path, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IFileSystemItem>>([]);
    }
}
