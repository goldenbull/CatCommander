using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class FileOperationConfirmWindow : Window
{
    public FileOperationConfirmWindow(FileOperationConfirmViewModel viewModel, ShortcutsSettings shortcuts)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts);

        // Close(object?) is what feeds a value back out through the caller's
        // ShowDialog<FileOperationMode?>(owner) await - see MainWindowViewModel.StartFileOperation.
        viewModel.RequestClose += mode => Close(mode);

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }
}
