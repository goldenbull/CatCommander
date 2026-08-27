using Avalonia.Controls;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class NewFolderWindow : Window
{
    public NewFolderWindow(NewFolderViewModel viewModel, ShortcutsSettings shortcuts, ShortcutInputContext? inputContext = null, ShortcutInputState? inputState = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts, inputContext, inputState);

        viewModel.RequestClose += Close;

        this.InstallEscapeToClose();

        // The OK button's IsDefault="True" alone doesn't catch Enter here: TextBox owns Enter
        // while it has focus (see TextEditKeyExceptions - Enter is a reserved text-editing
        // gesture) and never lets it bubble back up to the Window's default-button handling.
        this.InstallEnterSubmits(viewModel.CreateCommand);

        Opened += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }
}
