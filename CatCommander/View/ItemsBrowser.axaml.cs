using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class ItemsBrowser : UserControl
{
    private ItemsBrowserViewModel? ViewModel => DataContext as ItemsBrowserViewModel;
    private bool _filterWasHidden;

    public ItemsBrowser()
    {
        InitializeComponent();
        // DataContext is set from parent (MainPanel)

        // Add key down handler for space key selection and filter
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnPreviewTextInput, RoutingStrategies.Tunnel);
    }

    private void OnPreviewTextInput(object? sender, TextInputEventArgs e)
    {
        if (ViewModel == null || string.IsNullOrEmpty(e.Text))
            return;

        // If filter is not visible and we receive printable text input, show the filter
        if (!ViewModel.IsFilterVisible && !string.IsNullOrWhiteSpace(e.Text))
        {
            ViewModel.IsFilterVisible = true;

            // If we have existing filter text (was hidden with text), append; otherwise start new
            if (_filterWasHidden && !string.IsNullOrEmpty(ViewModel.FilterText))
            {
                // Continue with existing filter text (append new input)
                ViewModel.FilterText += e.Text;
            }
            else
            {
                // Start new filter
                ViewModel.FilterText = e.Text;
                _filterWasHidden = false;
            }

            tbFilter.Focus();
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel == null)
            return;

        // Handle Escape with two-stage behavior
        if (e.Key == Key.Escape && ViewModel.IsFilterVisible)
        {
            // Check if filter was previously hidden with text (second ESC scenario)
            if (_filterWasHidden && !string.IsNullOrEmpty(ViewModel.FilterText))
            {
                // Second ESC: clear filter text and hide
                ViewModel.FilterText = string.Empty;
                ViewModel.IsFilterVisible = false;
                _filterWasHidden = false;
            }
            else if (!string.IsNullOrEmpty(ViewModel.FilterText))
            {
                // First ESC: hide popup but keep filter text
                ViewModel.IsFilterVisible = false;
                _filterWasHidden = true;
            }
            else
            {
                // No filter text, just hide
                ViewModel.IsFilterVisible = false;
                _filterWasHidden = false;
            }
            e.Handled = true;
            return;
        }

        // Handle Space for selection only when filter is not visible
        if (e.Key == Key.Space && !ViewModel.IsFilterVisible)
        {
            ViewModel.ToggleCurrentItemSelection();
            e.Handled = true;
        }
    }

    private void OnClearFilterClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        // Clear filter text and hide popup
        ViewModel.FilterText = string.Empty;
        ViewModel.IsFilterVisible = false;
        _filterWasHidden = false;
    }
}
