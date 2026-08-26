using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class FileOperationProgressWindow : Window
{
    public FileOperationProgressWindow(FileOperationProgressViewModel viewModel, ShortcutsSettings shortcuts)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts);

        viewModel.RequestClose += Close;

        // Escape sends the job to the background rather than cancelling it - a bare dismiss
        // gesture destroying an in-progress copy/move would be an unpleasant surprise.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
