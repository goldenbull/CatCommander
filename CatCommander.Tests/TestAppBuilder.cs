using System;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using CatCommander.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CatCommander.Tests;

/// <summary>
/// Headless test host. The real App requires an IServiceProvider it doesn't need for view-only
/// tests, so this applies the same two style sources App.axaml does (FluentTheme + the vendored
/// TreeDataGrid's theme - without it, TreeDataGrid renders with no headers/rows, see App.axaml).
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://CatCommander.Tests/"))
        {
            Source = new Uri("avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"),
        });
    }
}
