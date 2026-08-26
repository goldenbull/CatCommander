using Avalonia.Controls;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class BatchRenameWindow : Window
{
    public BatchRenameWindow(BatchRenameViewModel viewModel, ShortcutsSettings shortcuts)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts);

        this.InstallEscapeToClose();
    }
}
