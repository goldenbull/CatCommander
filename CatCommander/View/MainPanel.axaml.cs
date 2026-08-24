using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class MainPanel : UserControl
{
    private MainPanelViewModel? ViewModel => DataContext as MainPanelViewModel;

    public MainPanel()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, OnGotFocus);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        ViewModel?.OnActivated?.Invoke();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Focusable="True" alone doesn't make a plain container grab focus on click - only
        // controls with their own click-to-focus behavior (TextBox, Button, ...) do that.
        // Request it explicitly; OnGotFocus above does the actual activation.
        Focus();
    }
}
