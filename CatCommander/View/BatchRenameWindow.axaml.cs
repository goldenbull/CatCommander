using Avalonia.Controls;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class BatchRenameWindow : Window
{
    public BatchRenameWindow(BatchRenameViewModel viewModel, ShortcutsSettings shortcuts, ShortcutInputContext? inputContext = null, ShortcutInputState? inputState = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts, inputContext, inputState, ShortcutScope.Dialog);

        this.InstallEscapeToClose();
    }
}
