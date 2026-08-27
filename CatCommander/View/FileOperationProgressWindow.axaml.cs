using Avalonia.Controls;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class FileOperationProgressWindow : Window
{
    public FileOperationProgressWindow(FileOperationProgressViewModel viewModel, ShortcutsSettings shortcuts, ShortcutInputContext? inputContext = null, ShortcutInputState? inputState = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts, inputContext, inputState);

        viewModel.RequestClose += Close;

        // Escape sends the job to the background rather than cancelling it - a bare dismiss
        // gesture destroying an in-progress copy/move would be an unpleasant surprise. Plain
        // Close() is exactly what "Send to Background" already does, so the default onEscape is
        // correct as-is.
        this.InstallEscapeToClose();
    }
}
