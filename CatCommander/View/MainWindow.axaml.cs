using Avalonia.Controls;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel, ShortcutsSettings shortcuts)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts);

        // LeftPanel/RightPanel's ItemBrowsers each already self-focus the moment they're attached
        // (see ItemBrowser.axaml.cs), but that race is decided purely by visual-tree construction
        // order, not by which panel the ViewModel actually considers active. Re-asserting it once
        // more here, after everything's attached and shown, makes the two agree deterministically.
        Opened += (_, _) => viewModel.ActivePanel?.RequestFocus();
    }
}
