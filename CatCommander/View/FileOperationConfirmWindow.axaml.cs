using Avalonia.Controls;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class FileOperationConfirmWindow : Window
{
    public FileOperationConfirmWindow(FileOperationConfirmViewModel viewModel, ShortcutsSettings shortcuts, ShortcutInputContext? inputContext = null, ShortcutInputState? inputState = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts, inputContext, inputState, ShortcutScope.Dialog);

        // Close(object?) is what feeds a value back out through the caller's
        // ShowDialog<FileOperationMode?>(owner) await - see MainWindowViewModel.StartFileOperation.
        viewModel.RequestClose += mode => Close(mode);

        // Escape means Cancel, i.e. a null result - not the parameterless Close() the default
        // onEscape would call.
        this.InstallEscapeToClose(() => Close(null));
        this.InstallEnterSubmits(viewModel.RunNowCommand);
    }
}
