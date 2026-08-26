using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Services;
using CatCommander.View;
using CatCommander.ViewModels;
using Xunit;

namespace CatCommander.Tests.View;

/// <summary>
/// Regression coverage for the bug where SwitchPanel (Tab) and SwitchTabInSamePanel (Ctrl+Tab)
/// updated MainWindowViewModel.ActivePanel/MainPanelViewModel.ActiveTab correctly but never moved
/// real Avalonia keyboard focus to match - so arrow keys (and Ctrl+Tab, which reads a possibly
/// stale ActivePanel) kept acting on whichever grid had focus before the switch, and a subsequent
/// GotFocus from that stale focus could even flip ActivePanel back. See ItemBrowser.axaml.cs's
/// FocusGrid/ApplyFocus and MainWindowViewModel.SetActivePanel/SwitchPanel.
/// </summary>
public class MainWindowFocusTests : IDisposable
{
    private readonly string _root;
    private readonly MainWindow _window;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowFocusTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderFocusTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var registry = new FileSystemProviderRegistry();
        registry.Register(new LocalFileSystemProviderFactory());
        var iconCache = new IconCache();
        ItemBrowserViewModel ItemBrowserFactory() => new(registry, iconCache);
        MainPanelViewModel MainPanelFactory() => new(ItemBrowserFactory);

        var configManager = new ConfigManager();
        _viewModel = new MainWindowViewModel(configManager, new FileOperationQueue(), MainPanelFactory, () => null!, () => null!, () => null!);

        _window = new MainWindow(_viewModel, configManager.Shortcuts) { Width = 1024, Height = 640 };
        _window.Show();
        Pump();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // NavigateToAsync does real (if fast, local) async I/O - MainPanelViewModel's own constructor
    // already fires one off (to UserProfile) per panel, fire-and-forget, same as production
    // startup. Awaiting our own follow-up navigation here, rather than also firing-and-forgetting
    // it, is what actually guarantees both are done before a test starts pressing keys: Pump()
    // alone only drains whatever's *already* been posted to the dispatcher, not pending Task
    // continuations that haven't resumed yet, so a fire-and-forget navigation could still land
    // (and steal focus - see ItemBrowserViewModel.RequestFocus) in the middle of a test. This is
    // what made these tests genuinely flaky before this fix, not a bug in the app itself.
    private async Task EnsureNavigatedAsync()
    {
        await _viewModel.LeftPanel.ActiveTab!.NavigateToAsync(_root);
        await _viewModel.RightPanel.ActiveTab!.NavigateToAsync(_root);
        Pump();
    }

    // Each RunJobs() drain can itself queue more work (ApplyFocus -> GotFocus -> more posted
    // work), so a small fixed iteration count is flaky - it sometimes returns before everything
    // has actually settled. Loop generously instead of guessing a minimal count.
    private static void Pump()
    {
        for (var i = 0; i < 50; i++)
            Dispatcher.UIThread.RunJobs();
    }

    private TreeDataGrid GetGrid(MainPanelViewModel panel) =>
        _window.GetVisualDescendants().OfType<MainPanel>()
            .Single(p => ReferenceEquals(p.DataContext, panel))
            .GetVisualDescendants().OfType<TreeDataGrid>().Single();

    [AvaloniaFact]
    public async Task SwitchPanel_MovesRealKeyboardFocus_ToTheNewlyActivePanelsGrid()
    {
        await EnsureNavigatedAsync();

        var leftGrid = GetGrid(_viewModel.LeftPanel);
        var rightGrid = GetGrid(_viewModel.RightPanel);
        leftGrid.Focus();
        Pump();
        Assert.True(leftGrid.IsFocused);

        var focusRequestedFired = false;
        _viewModel.RightPanel.ActiveTab!.FocusRequested += () => focusRequestedFired = true;

        _window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Pump();

        Assert.Same(_viewModel.RightPanel, _viewModel.ActivePanel);
        Assert.True(focusRequestedFired, "expected RightPanel.ActiveTab.FocusRequested to fire");
        Assert.True(rightGrid.IsFocused, "expected real keyboard focus to move to the right panel's grid");
        Assert.False(leftGrid.IsFocused);
    }

    [AvaloniaFact]
    public async Task SwitchPanel_TogglesTheActiveClass_OnBothGrids()
    {
        // Regression: the inactive panel's selection stayed navy instead of turning the classic
        // TC muted gray. Root cause was a `TreeDataGrid:not(.active) ...` selector not reliably
        // reacting to Classes.active toggling - fixed by flipping to a positive-only
        // `TreeDataGrid.active ...` selector (see ItemBrowser.axaml). This test pins down the one
        // fact that selector choice depends on: FileGrid's own "active" class actually flips.
        await EnsureNavigatedAsync();

        var leftGrid = GetGrid(_viewModel.LeftPanel);
        var rightGrid = GetGrid(_viewModel.RightPanel);
        leftGrid.Focus();
        Pump();

        Assert.Contains("active", leftGrid.Classes);
        Assert.DoesNotContain("active", rightGrid.Classes);

        _window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Pump();

        Assert.DoesNotContain("active", leftGrid.Classes);
        Assert.Contains("active", rightGrid.Classes);
    }

    [AvaloniaFact]
    public async Task ActivePanelCursor_IsPaleBlue_InactivePanelCursor_IsGray()
    {
        // Regression: the first fix (a positive TreeDataGrid.active selector on the *cell*) still
        // left the active panel's selection looking gray - a different gray than the inactive
        // panel's, because TreeDataGridRow's own ControlTheme paints
        // TreeDataGridSelectedCellBackgroundBrush onto its PART_CellsPresenter *underneath* the
        // cells, and only the cell-level layer was being overridden. Asserts the actual resolved
        // Background color on that presenter, not just that a style/class exists.
        // A row has to exist to select - unlike CtrlUpThenCtrlTab_ViaRealKeypresses_EndToEnd, this
        // test doesn't otherwise care what it is, just that index 0 resolves to something real.
        Directory.CreateDirectory(Path.Combine(_root, "subdir"));
        await EnsureNavigatedAsync();

        var leftGrid = GetGrid(_viewModel.LeftPanel);
        var rightGrid = GetGrid(_viewModel.RightPanel);

        ((ITreeDataGridRowSelectionModel)_viewModel.LeftPanel.ActiveTab!.Source!.Selection!).Select(new IndexPath(0));
        ((ITreeDataGridRowSelectionModel)_viewModel.RightPanel.ActiveTab!.Source!.Selection!).Select(new IndexPath(0));
        Pump();

        leftGrid.Focus();
        Pump();
        Assert.Contains("active", leftGrid.Classes);
        Assert.DoesNotContain("active", rightGrid.Classes);

        // TcCursorColor / TcInactiveSelectionColor - see ClassicTheme.axaml. The cursor color is
        // deliberately pale (not the navy MarkedToBackgroundBrushConverter uses for marked rows) -
        // see ItemBrowser.axaml's own comment on why that no longer needs a white-text override.
        Assert.Equal(Color.Parse("#D1E7FD"), SelectedRowPresenterBackgroundColor(leftGrid));
        Assert.Equal(Color.Parse("#C0C0C0"), SelectedRowPresenterBackgroundColor(rightGrid));
    }

    private static Color SelectedRowPresenterBackgroundColor(TreeDataGrid grid)
    {
        var row = grid.GetVisualDescendants().OfType<TreeDataGridRow>().Single(r => r.IsSelected);
        var presenter = row.GetVisualDescendants().OfType<TreeDataGridCellsPresenter>().Single();
        return Assert.IsType<SolidColorBrush>(presenter.Background).Color;
    }

    [AvaloniaFact]
    public async Task SwitchTabInSamePanel_CyclesTabAndMovesRealKeyboardFocus_ToTheNewTabsGrid()
    {
        await EnsureNavigatedAsync();

        var firstTab = _viewModel.LeftPanel.ActiveTab!;
        var secondTab = new ItemBrowserViewModel(new FileSystemProviderRegistry(), new IconCache());
        _viewModel.LeftPanel.Tabs.Add(secondTab); // doesn't change ActiveTab - still firstTab
        Pump();

        GetGrid(_viewModel.LeftPanel).Focus();
        Pump();
        Assert.Same(_viewModel.LeftPanel, _viewModel.ActivePanel);

        _window.KeyPress(Key.Tab, RawInputModifiers.Control, PhysicalKey.Tab, null);
        Pump();

        Assert.Same(secondTab, _viewModel.LeftPanel.ActiveTab);
        Assert.True(GetGrid(_viewModel.LeftPanel).IsFocused, "expected real keyboard focus to move to the newly-active tab's grid");
    }

    [AvaloniaFact]
    public async Task CtrlUpThenCtrlTab_ViaRealKeypresses_EndToEnd()
    {
        // Fully end-to-end, unlike SwitchTabInSamePanel_CyclesTabAndMovesRealKeyboardFocus_ToTheNewTabsGrid
        // above (which manually adds a second tab): creates the second tab the same way a real user
        // does - select a folder, Cmd+Up/Ctrl+Up (OpenSelectedFolderInNewTab) - then Ctrl+Tab, to
        // rule out anything specific to that real path that the simplified version might miss.
        Directory.CreateDirectory(Path.Combine(_root, "subdir"));
        await EnsureNavigatedAsync();

        GetGrid(_viewModel.LeftPanel).Focus();
        Pump();
        Assert.Same(_viewModel.LeftPanel, _viewModel.ActivePanel);

        var firstTab = _viewModel.LeftPanel.ActiveTab!;
        var rowSelection = (Avalonia.Controls.Selection.ITreeDataGridRowSelectionModel)firstTab.Source!.Selection!;
        rowSelection.Select(new Avalonia.Controls.IndexPath(0)); // the only row: "subdir"
        Pump();

        var openInNewTabGesture = CatCommander.Config.ShortcutsSettings.CurrentStyle == CatCommander.Config.KeyboardStyle.MacOS
            ? (Key.Up, RawInputModifiers.Meta, PhysicalKey.ArrowUp)
            : (Key.Up, RawInputModifiers.Control, PhysicalKey.ArrowUp);
        _window.KeyPress(openInNewTabGesture.Item1, openInNewTabGesture.Item2, openInNewTabGesture.Item3, null);
        Pump();

        Assert.Equal(2, _viewModel.LeftPanel.Tabs.Count);
        var secondTab = _viewModel.LeftPanel.ActiveTab!;
        Assert.NotSame(firstTab, secondTab);

        _window.KeyPress(Key.Tab, RawInputModifiers.Control, PhysicalKey.Tab, null);
        Pump();

        Assert.Same(firstTab, _viewModel.LeftPanel.ActiveTab);
    }

    [AvaloniaFact]
    public async Task NewTab_GetsAProperlySizedStarColumn()
    {
        Directory.CreateDirectory(Path.Combine(_root, "subdir"));
        await EnsureNavigatedAsync();

        GetGrid(_viewModel.LeftPanel).Focus();
        Pump();

        var firstTab = _viewModel.LeftPanel.ActiveTab!;
        var rowSelection = (Avalonia.Controls.Selection.ITreeDataGridRowSelectionModel)firstTab.Source!.Selection!;
        rowSelection.Select(new Avalonia.Controls.IndexPath(0)); // the only row: "subdir"
        Pump();

        var openInNewTabGesture = CatCommander.Config.ShortcutsSettings.CurrentStyle == CatCommander.Config.KeyboardStyle.MacOS
            ? (Key.Up, RawInputModifiers.Meta, PhysicalKey.ArrowUp)
            : (Key.Up, RawInputModifiers.Control, PhysicalKey.ArrowUp);
        _window.KeyPress(openInNewTabGesture.Item1, openInNewTabGesture.Item2, openInNewTabGesture.Item3, null);
        Pump();

        var secondTab = _viewModel.LeftPanel.ActiveTab!;
        Assert.NotSame(firstTab, secondTab);

        // OpenSelectedFolderInNewTab's own navigation is fire-and-forget (same reasoning as
        // GoBackToParentFolder - it's reached via a synchronous ICommand from the real keypress
        // above). Wait for it to actually land instead of trusting a fixed Pump() count.
        for (var i = 0; i < 100 && secondTab.Source is null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Pump();
        }

        var nameColumnWidth = secondTab.Source!.Columns[0].ActualWidth;
        Assert.True(nameColumnWidth > 100, $"expected the new tab's Star-width Name column to fill the available width, got {nameColumnWidth}");
    }

    [AvaloniaFact]
    public async Task SwitchPanel_ThenArrowKey_DoesNotRevertActivePanel()
    {
        // Regression for the exact user-reported sequence: switching to the right panel, then
        // pressing Up/Down, flipped ActivePanel back to left - because real focus had never
        // actually left the left grid, so the arrow key's own row navigation (handled by
        // TreeDataGrid itself, not a bound Operation) re-fired GotFocus there.
        await EnsureNavigatedAsync();

        GetGrid(_viewModel.LeftPanel).Focus();
        Pump();

        _window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Pump();
        Assert.Same(_viewModel.RightPanel, _viewModel.ActivePanel);

        _window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Pump();

        Assert.Same(_viewModel.RightPanel, _viewModel.ActivePanel);
    }

    [AvaloniaFact]
    public async Task GotoLastItem_ScrollsTheNewCurrentRowIntoView()
    {
        // Regression: GotoFirstItem/GotoLastItem (Home/End) moved the selection correctly but left
        // the scroll position wherever it already was, because setting SelectedIndex directly
        // (ItemBrowserViewModel.SetCurrentRow) bypasses TreeDataGrid's own keyboard handling - the
        // only thing that normally calls RowsPresenter.BringIntoView. With enough rows to overflow
        // a small window, the last row only ends up *realized* (virtualization keeps far-off rows
        // unrealized) if it was actually scrolled into view.
        for (var i = 0; i < 100; i++)
            Directory.CreateDirectory(Path.Combine(_root, $"dir{i:D3}"));
        await EnsureNavigatedAsync();

        var leftGrid = GetGrid(_viewModel.LeftPanel);
        leftGrid.Focus();
        Pump();

        var lastRowIndex = _viewModel.LeftPanel.ActiveTab!.Source!.Rows.Count - 1;
        Assert.True(lastRowIndex > 20, "expected enough rows to overflow the window and require scrolling");
        Assert.DoesNotContain(
            leftGrid.GetVisualDescendants().OfType<TreeDataGridRow>(),
            row => row.RowIndex == lastRowIndex);

        _window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
        Pump();

        Assert.Contains(
            leftGrid.GetVisualDescendants().OfType<TreeDataGridRow>(),
            row => row.RowIndex == lastRowIndex);
    }

    [AvaloniaFact]
    public async Task Space_TogglesMark_UnlessAFilterIsAlreadyBeingTyped_WhereItsAWordSeparator()
    {
        Directory.CreateDirectory(Path.Combine(_root, "alpha"));
        Directory.CreateDirectory(Path.Combine(_root, "beta"));
        await EnsureNavigatedAsync();

        var leftGrid = GetGrid(_viewModel.LeftPanel);
        leftGrid.Focus();
        Pump();

        var tab = _viewModel.LeftPanel.ActiveTab!;
        Assert.Equal(0, tab.SelectedFolderCount);

        // No filter active - Space marks the cursor row ("alpha", row 0).
        _window.KeyTextInput(" ");
        Pump();

        Assert.False(tab.IsFilterActive);
        Assert.Equal(1, tab.SelectedFolderCount);

        // Start a filter - "alpha" stays visible (and thus stays marked) throughout.
        _window.KeyTextInput("al");
        Pump();

        Assert.True(tab.IsFilterActive);
        Assert.Equal("al", tab.FilterText);
        Assert.Equal(1, tab.SelectedFolderCount);

        // Now Space is a word separator, not a mark toggle - it's appended to the filter text and
        // the earlier mark is left untouched.
        _window.KeyTextInput(" ");
        Pump();

        Assert.Equal("al ", tab.FilterText);
        Assert.Equal(1, tab.SelectedFolderCount);
    }
}
