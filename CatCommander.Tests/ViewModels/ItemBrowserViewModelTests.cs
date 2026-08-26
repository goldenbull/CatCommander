using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Selection;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Services;
using CatCommander.ViewModels;
using Xunit;

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
    public async Task NavigateToAsync_RecordsHistory_MostRecentFirst_Deduplicated()
    {
        var vm = CreateViewModel();
        await vm.NavigateToAsync(_root);
        await vm.NavigateToAsync(_child);
        await vm.NavigateToAsync(_root); // revisiting should move it back to front, not duplicate it

        Assert.Equal(new[] { _root, _child }, vm.NavigationHistory);
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
}
