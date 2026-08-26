using System;
using System.IO;
using System.Threading.Tasks;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Services;
using CatCommander.ViewModels;
using Xunit;

namespace CatCommander.Tests.ViewModels;

public class MainWindowViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderMWVMTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var registry = new FileSystemProviderRegistry();
        registry.Register(new LocalFileSystemProviderFactory());
        var iconCache = new IconCache();
        MainPanelViewModel MainPanelFactory() => new(() => new ItemBrowserViewModel(registry, iconCache));

        _viewModel = new MainWindowViewModel(new ConfigManager(), new FileOperationQueue(), MainPanelFactory, () => null!, () => null!, () => null!);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    private async Task EnsureBothPanelsNavigatedAsync()
    {
        await WaitUntilAsync(() =>
            !string.IsNullOrEmpty(_viewModel.LeftPanel.ActiveTab!.CurrentPath)
            && !string.IsNullOrEmpty(_viewModel.RightPanel.ActiveTab!.CurrentPath));
    }

    [Fact]
    public async Task OpenCurrentFolderInOppositePanel_FromLeftPanel_OpensTheSelectedFolder_InANewRightPanelTab()
    {
        // Regression: this must open the *selected* row, not CurrentPath (the directory the tab
        // is browsing) - using CurrentPath would open the parent of what's actually highlighted.
        var selectedChild = Path.Combine(_root, "child");
        Directory.CreateDirectory(selectedChild);

        await EnsureBothPanelsNavigatedAsync();

        // LeftPanel is ActivePanel by default - see MainWindowViewModel's constructor. Navigating
        // to _root auto-selects its first (and only) row - "child" - per RebuildSource.
        await _viewModel.LeftPanel.ActiveTab!.NavigateToAsync(_root);
        var originalRightTab = _viewModel.RightPanel.ActiveTab;

        _viewModel.GetCommand(Operation.OpenCurrentFolderInOppositePanel)!.Execute(null);
        await WaitUntilAsync(() => _viewModel.RightPanel.Tabs.Count == 2);

        Assert.Equal(2, _viewModel.RightPanel.Tabs.Count);
        Assert.NotSame(originalRightTab, _viewModel.RightPanel.ActiveTab);
        Assert.Contains(originalRightTab, _viewModel.RightPanel.Tabs);
        await WaitUntilAsync(() => _viewModel.RightPanel.ActiveTab!.CurrentPath == selectedChild);
        Assert.Equal(selectedChild, _viewModel.RightPanel.ActiveTab!.CurrentPath);
    }

    [Fact]
    public async Task OpenCurrentFolderInOppositePanel_FromRightPanel_OpensTheSelectedFolder_InANewLeftPanelTab()
    {
        var selectedChild = Path.Combine(_root, "child");
        Directory.CreateDirectory(selectedChild);

        await EnsureBothPanelsNavigatedAsync();

        _viewModel.GetCommand(Operation.SwitchPanel)!.Execute(null);
        Assert.Same(_viewModel.RightPanel, _viewModel.ActivePanel);

        await _viewModel.RightPanel.ActiveTab!.NavigateToAsync(_root);
        var originalLeftTab = _viewModel.LeftPanel.ActiveTab;

        _viewModel.GetCommand(Operation.OpenCurrentFolderInOppositePanel)!.Execute(null);
        await WaitUntilAsync(() => _viewModel.LeftPanel.Tabs.Count == 2);

        Assert.Equal(2, _viewModel.LeftPanel.Tabs.Count);
        Assert.NotSame(originalLeftTab, _viewModel.LeftPanel.ActiveTab);
        Assert.Contains(originalLeftTab, _viewModel.LeftPanel.Tabs);
        await WaitUntilAsync(() => _viewModel.LeftPanel.ActiveTab!.CurrentPath == selectedChild);
        Assert.Equal(selectedChild, _viewModel.LeftPanel.ActiveTab!.CurrentPath);
    }

    [Fact]
    public async Task OpenCurrentFolderInOppositePanel_WithNoEnterableSelection_DoesNothing()
    {
        // _root has no children, so after navigating there's nothing to select - CanEnter has
        // nothing to say yes to, and there must be no fallback to CurrentPath (see the regression
        // above).
        await EnsureBothPanelsNavigatedAsync();

        await _viewModel.LeftPanel.ActiveTab!.NavigateToAsync(_root);
        var originalRightTab = _viewModel.RightPanel.ActiveTab;

        _viewModel.GetCommand(Operation.OpenCurrentFolderInOppositePanel)!.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Single(_viewModel.RightPanel.Tabs);
        Assert.Same(originalRightTab, _viewModel.RightPanel.ActiveTab);
    }
}
