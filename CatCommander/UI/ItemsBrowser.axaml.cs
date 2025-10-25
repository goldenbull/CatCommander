using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.ViewModels;

namespace CatCommander.UI;

public partial class ItemsBrowser : UserControl
{
    public ItemsBrowser()
    {
        InitializeComponent();
        DataContext = new ItemsBrowserViewModel();

        // Add key down handler for space key selection
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && DataContext is ItemsBrowserViewModel viewModel)
        {
            viewModel.ToggleCurrentItemSelection();
            e.Handled = true;
        }
    }
}
