using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CatCommander.FileSystem;
using CatCommander.Services;
using CatCommander.View;
using CatCommander.ViewModels;
using Xunit;

namespace CatCommander.Tests.View;

/// <summary>
/// Regression coverage for the bug where TreeDataGrid rendered with no headers/rows at all
/// because the vendored control's Themes/Fluent.axaml was never merged into App.axaml - a plain
/// build/run doesn't catch this (no exception, ItemBrowserViewModel's Source/totals are all
/// populated correctly), only actually looking at the rendered visual tree does.
/// </summary>
public class ItemBrowserRenderTests : IDisposable
{
    private readonly string _root;

    public ItemBrowserRenderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderRenderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "hello");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [AvaloniaFact]
    public async Task ItemBrowser_RendersColumnHeadersAndRows_AfterNavigating()
    {
        var registry = new FileSystemProviderRegistry();
        registry.Register(new LocalFileSystemProviderFactory());
        var viewModel = new ItemBrowserViewModel(registry, new IconCache());
        await viewModel.NavigateToAsync(_root);

        var view = new ItemBrowser { DataContext = viewModel };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var grid = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        var headers = grid.GetVisualDescendants().OfType<TreeDataGridColumnHeadersPresenter>().SingleOrDefault();
        var rows = grid.GetVisualDescendants().OfType<TreeDataGridRowsPresenter>().SingleOrDefault();

        Assert.NotNull(headers);
        Assert.NotNull(rows);
        Assert.True(headers.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Any(), "expected realized column headers");
        Assert.True(rows.GetVisualDescendants().OfType<TreeDataGridRow>().Any(), "expected realized rows");
    }
}
