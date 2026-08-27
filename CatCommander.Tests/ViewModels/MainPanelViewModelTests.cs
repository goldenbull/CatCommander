using System;
using System.IO;
using System.Threading.Tasks;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Services;
using CatCommander.ViewModels;
using Xunit;

namespace CatCommander.Tests.ViewModels;

public class MainPanelViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemProviderRegistry _registry;
    private readonly IconCache _iconCache = new();

    public MainPanelViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderMPVMTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _registry = new FileSystemProviderRegistry();
        _registry.Register(new LocalFileSystemProviderFactory());
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private MainPanelViewModel CreatePanel() =>
        new(() => new ItemBrowserViewModel(_registry, _iconCache));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CloseTab_WithMultipleTabs_RemovesTheActiveOne_AndActivatesTheSiblingToItsLeft()
    {
        var childA = Path.Combine(_root, "a");
        var childB = Path.Combine(_root, "b");
        Directory.CreateDirectory(childA);
        Directory.CreateDirectory(childB);

        var panel = CreatePanel();
        var firstTab = panel.ActiveTab!;
        await WaitUntilAsync(() => !string.IsNullOrEmpty(firstTab.CurrentPath));

        var secondTab = new ItemBrowserViewModel(_registry, _iconCache);
        panel.Tabs.Add(secondTab);
        await secondTab.NavigateToAsync(childA);

        var thirdTab = new ItemBrowserViewModel(_registry, _iconCache);
        panel.Tabs.Add(thirdTab);
        await thirdTab.NavigateToAsync(childB);

        panel.SelectTabCommand.Execute(secondTab);
        Assert.Same(secondTab, panel.ActiveTab);

        panel.GetCommand(Operation.CloseTab)!.Execute(null);

        Assert.Equal(2, panel.Tabs.Count);
        Assert.DoesNotContain(secondTab, panel.Tabs);
        Assert.Same(firstTab, panel.ActiveTab);
    }

    [Fact]
    public async Task CloseTab_WithOnlyOneTab_DoesNotRemoveIt_ButResetsItToHomePath()
    {
        var panel = CreatePanel();
        var onlyTab = panel.ActiveTab!;
        await WaitUntilAsync(() => !string.IsNullOrEmpty(onlyTab.CurrentPath));

        await onlyTab.NavigateToAsync(_root);
        Assert.Equal(_root, onlyTab.CurrentPath);

        panel.GetCommand(Operation.CloseTab)!.Execute(null);
        await WaitUntilAsync(() => onlyTab.CurrentPath != _root);

        Assert.Single(panel.Tabs);
        Assert.Same(onlyTab, panel.ActiveTab);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), onlyTab.CurrentPath);
    }

    [Fact]
    public async Task Marks_AreIndependentPerTab_UnlikeDirectorySelectionWhichNeverPersists()
    {
        var childA = Path.Combine(_root, "a");
        Directory.CreateDirectory(childA);

        var panel = CreatePanel();
        var firstTab = panel.ActiveTab!;
        await WaitUntilAsync(() => !string.IsNullOrEmpty(firstTab.CurrentPath));
        await firstTab.NavigateToAsync(_root);
        firstTab.ToggleMarkCurrentItem(); // marks "a" in the first tab

        var secondTab = new ItemBrowserViewModel(_registry, _iconCache);
        panel.Tabs.Add(secondTab);
        await secondTab.NavigateToAsync(_root);

        Assert.Equal(1, firstTab.SelectedFolderCount);
        Assert.Equal(0, secondTab.SelectedFolderCount);
    }

    [Fact]
    public async Task RestoreSession_RecreatesTabsAndActiveTab()
    {
        var childA = Path.Combine(_root, "a");
        var childB = Path.Combine(_root, "b");
        Directory.CreateDirectory(childA);
        Directory.CreateDirectory(childB);
        var panel = CreatePanel();

        panel.RestoreSession(new PanelSessionState
        {
            Tabs = [childA, childB],
            ActiveTab = 1,
        });
        await WaitUntilAsync(() => panel.Tabs.All(tab => !string.IsNullOrEmpty(tab.CurrentPath)));

        Assert.Equal(2, panel.Tabs.Count);
        Assert.Equal(childA, panel.Tabs[0].CurrentPath);
        Assert.Equal(childB, panel.Tabs[1].CurrentPath);
        Assert.Same(panel.Tabs[1], panel.ActiveTab);
    }
}
