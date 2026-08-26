using Avalonia.Controls;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class FindWindow : Window
{
    public FindWindow(FindViewModel viewModel, ShortcutsSettings shortcuts)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts);

        this.InstallEscapeToClose();
    }
}
