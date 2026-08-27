using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.View;
using CatCommander.ViewModels;

namespace CatCommander.Tests.View;

public sealed class DialogKeyboardTests
{
    [AvaloniaFact]
    public void FileOperationConfirmation_EnterRunsDefaultAction()
    {
        var settings = new ShortcutsSettings();
        settings.RebuildNormalized(ShortcutsSettings.CurrentStyle);
        var viewModel = new FileOperationConfirmViewModel(FileOperationKind.Copy, 1, "/destination");
        var inputContext = new ShortcutInputContext();
        FileOperationMode? result = null;
        viewModel.RequestClose += value => result = value;
        var window = new FileOperationConfirmWindow(viewModel, settings, inputContext);
        window.Show();

        Assert.True(ShortcutRoutingPolicy.ShouldYieldToWindowConvention(
            new KeyGesture(Key.Enter), ShortcutScope.Dialog));
        Assert.False(ShortcutRoutingPolicy.ShouldYieldToWindowConvention(
            new KeyGesture(Key.Enter, KeyModifiers.Meta), ShortcutScope.Dialog));

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Equal(FileOperationMode.RunNow, result);
        window.Close();
    }
}
