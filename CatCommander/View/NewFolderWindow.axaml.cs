using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class NewFolderWindow : Window
{
    private readonly NewFolderViewModel _viewModel;

    public NewFolderWindow(NewFolderViewModel viewModel, ShortcutsSettings shortcuts)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        ShortcutRouter.Install(this, shortcuts);

        viewModel.RequestClose += Close;

        // Escape-to-close and Enter-to-submit are universal dialog conventions, not
        // user-configurable Operations - handled directly here, after ShortcutRouter so a real
        // Operation bound to either (if any) still gets first refusal. Enter needs this explicit
        // handling despite the OK button's IsDefault="True": TextBox owns Enter while it has focus
        // (see TextEditKeyExceptions - Enter is a reserved text-editing gesture) and never lets it
        // bubble back up to the Window's default-button handling.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        Opened += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _viewModel.CreateCommand.CanExecute(null))
        {
            _viewModel.CreateCommand.Execute(null);
            e.Handled = true;
        }
    }
}
