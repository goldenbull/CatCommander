using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class ItemBrowser : UserControl
{
    private ItemBrowserViewModel? vm => DataContext as ItemBrowserViewModel;

    private bool editingFilter;

    public ItemBrowser()
    {
        InitializeComponent();
        // DataContext is set from parent (MainPanel)

        // Add key down handler for space key selection and filter
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnPreviewTextInput, RoutingStrategies.Tunnel);
    }

    private void OnPreviewTextInput(object? sender, TextInputEventArgs e)
    {
        if (vm == null || string.IsNullOrEmpty(e.Text))
            return;

        // If we receive printable text input, means we are editing the filter
        if (!string.IsNullOrWhiteSpace(e.Text))
        {
            editingFilter = true;
            tbFilter.Focus();
            vm.FilterText += e.Text;
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (vm == null)
            return;

        // Handle Escape with two-stage behavior
        if (e.Key == Key.Escape)
        {
            if (editingFilter)
            {
                // exit filter edit
                grid.Focus();
                editingFilter = false;
            }
            else
            {
                // clear filter
                vm.FilterText = string.Empty;
            }

            e.Handled = true;
        }
    }
}