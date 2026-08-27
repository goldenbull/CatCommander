using Avalonia.Controls;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class JobListWindow : Window
{
    public JobListWindow(JobListViewModel viewModel, ShortcutsSettings shortcuts, ShortcutInputContext? inputContext = null, ShortcutInputState? inputState = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts, inputContext, inputState);

        this.InstallEscapeToClose();
    }
}
