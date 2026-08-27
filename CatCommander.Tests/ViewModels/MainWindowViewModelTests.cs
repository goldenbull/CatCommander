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
    private readonly string _configDirectory;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderMWVMTests_" + Guid.NewGuid().ToString("N"));
        _configDirectory = Path.Combine(Path.GetTempPath(), "CatCommanderMWVMConfig_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var registry = new FileSystemProviderRegistry();
        registry.Register(new LocalFileSystemProviderFactory());
        var iconCache = new IconCache();
        MainPanelViewModel MainPanelFactory() => new(() => new ItemBrowserViewModel(registry, iconCache));

        _viewModel = new MainWindowViewModel(
            new ConfigManager(_configDirectory),
            new FileOperationQueue(), MainPanelFactory, () => null!, () => null!, () => null!);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        if (Directory.Exists(_configDirectory))
            Directory.Delete(_configDirectory, recursive: true);
    }

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
    public async Task Constructor_RestoresBothPanelsTabsAndActivePanel()
    {
        var leftA = Path.Combine(_root, "left-a");
        var leftB = Path.Combine(_root, "left-b");
        var right = Path.Combine(_root, "right");
        Directory.CreateDirectory(leftA);
        Directory.CreateDirectory(leftB);
        Directory.CreateDirectory(right);
        var config = new ConfigManager(Path.Combine(_root, "restore-config"));
        config.SaveSession(new SessionState
        {
            ActivePanel = "right",
            Left = new PanelSessionState { Tabs = [leftA, leftB], ActiveTab = 1 },
            Right = new PanelSessionState { Tabs = [right], ActiveTab = 0 },
        });
        var registry = new FileSystemProviderRegistry();
        registry.Register(new LocalFileSystemProviderFactory());
        var icons = new IconCache();
        MainPanelViewModel PanelFactory() => new(() => new ItemBrowserViewModel(registry, icons));

        var restored = new MainWindowViewModel(
            config, new FileOperationQueue(), PanelFactory, () => null!, () => null!, () => null!);
        await WaitUntilAsync(() => restored.LeftPanel.Tabs.All(tab => !string.IsNullOrEmpty(tab.CurrentPath)) &&
                                   restored.RightPanel.Tabs.All(tab => !string.IsNullOrEmpty(tab.CurrentPath)));

        Assert.Same(restored.RightPanel, restored.ActivePanel);
        Assert.Equal(2, restored.LeftPanel.Tabs.Count);
        Assert.Equal(leftB, restored.LeftPanel.ActiveTab!.CurrentPath);
        Assert.Equal(right, restored.RightPanel.ActiveTab!.CurrentPath);
    }

    [Fact]
    public async Task OpenCurrentFolderInRightPanel_FromLeftPanel_OpensTheSelectedFolder_InANewRightPanelTab()
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

        _viewModel.GetCommand(Operation.OpenCurrentFolderInRightPanel)!.Execute(null);
        await WaitUntilAsync(() => _viewModel.RightPanel.Tabs.Count == 2);

        Assert.Equal(2, _viewModel.RightPanel.Tabs.Count);
        Assert.NotSame(originalRightTab, _viewModel.RightPanel.ActiveTab);
        Assert.Contains(originalRightTab, _viewModel.RightPanel.Tabs);
        await WaitUntilAsync(() => _viewModel.RightPanel.ActiveTab!.CurrentPath == selectedChild);
        Assert.Equal(selectedChild, _viewModel.RightPanel.ActiveTab!.CurrentPath);
    }

    [Fact]
    public async Task OpenCurrentFolderInLeftPanel_WhileLeftPanelIsActive_IsUnavailable()
    {
        await EnsureBothPanelsNavigatedAsync();

        Assert.Same(_viewModel.LeftPanel, _viewModel.ActivePanel);
        Assert.Null(_viewModel.GetCommand(Operation.OpenCurrentFolderInLeftPanel));
    }

    [Fact]
    public async Task OpenCurrentFolderInLeftPanel_FromRightPanel_OpensTheSelectedFolder_InANewLeftPanelTab()
    {
        var selectedChild = Path.Combine(_root, "child");
        Directory.CreateDirectory(selectedChild);

        await EnsureBothPanelsNavigatedAsync();

        _viewModel.GetCommand(Operation.SwitchPanel)!.Execute(null);
        Assert.Same(_viewModel.RightPanel, _viewModel.ActivePanel);

        await _viewModel.RightPanel.ActiveTab!.NavigateToAsync(_root);
        var originalLeftTab = _viewModel.LeftPanel.ActiveTab;

        _viewModel.GetCommand(Operation.OpenCurrentFolderInLeftPanel)!.Execute(null);
        await WaitUntilAsync(() => _viewModel.LeftPanel.Tabs.Count == 2);

        Assert.Equal(2, _viewModel.LeftPanel.Tabs.Count);
        Assert.NotSame(originalLeftTab, _viewModel.LeftPanel.ActiveTab);
        Assert.Contains(originalLeftTab, _viewModel.LeftPanel.Tabs);
        await WaitUntilAsync(() => _viewModel.LeftPanel.ActiveTab!.CurrentPath == selectedChild);
        Assert.Equal(selectedChild, _viewModel.LeftPanel.ActiveTab!.CurrentPath);
    }

    [Fact]
    public async Task OpenCurrentFolderInRightPanel_WhileRightPanelIsActive_IsUnavailable()
    {
        await EnsureBothPanelsNavigatedAsync();
        _viewModel.GetCommand(Operation.SwitchPanel)!.Execute(null);

        Assert.Same(_viewModel.RightPanel, _viewModel.ActivePanel);
        Assert.Null(_viewModel.GetCommand(Operation.OpenCurrentFolderInRightPanel));
    }

    [Fact]
    public async Task OpenCurrentFolderInRightPanel_WithNoEnterableSelection_DoesNothing()
    {
        // _root has no children, so after navigating there's nothing to select - CanEnter has
        // nothing to say yes to, and there must be no fallback to CurrentPath (see the regression
        // above).
        await EnsureBothPanelsNavigatedAsync();

        await _viewModel.LeftPanel.ActiveTab!.NavigateToAsync(_root);
        var originalRightTab = _viewModel.RightPanel.ActiveTab;

        _viewModel.GetCommand(Operation.OpenCurrentFolderInRightPanel)!.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Single(_viewModel.RightPanel.Tabs);
        Assert.Same(originalRightTab, _viewModel.RightPanel.ActiveTab);
    }
}
