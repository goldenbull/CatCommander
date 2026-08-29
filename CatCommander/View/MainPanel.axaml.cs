using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class MainPanel : UserControl
{
    private MainPanelViewModel? ViewModel => DataContext as MainPanelViewModel;
    private MainPanelViewModel? _subscribedViewModel;

    public MainPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AddHandler(GotFocusEvent, OnGotFocus);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ShowFavoritesRequested -= ShowFavorites;
            _subscribedViewModel.HideFavoritesRequested -= HideFavorites;
        }
        _subscribedViewModel = ViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ShowFavoritesRequested += ShowFavorites;
            _subscribedViewModel.HideFavoritesRequested += HideFavorites;
        }
    }

    private void ShowFavorites() => FavoritesButton.Flyout?.ShowAt(FavoritesButton);
    private void HideFavorites() => FavoritesButton.Flyout?.Hide();

    private void OnFavoritesOpened(object? sender, EventArgs e)
    {
        FavoritesList.SelectedIndex = FavoritesList.ItemCount > 0 ? 0 : -1;
        FavoritesList.Focus();
    }

    private void OnFavoritesKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelectedFavorite();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideFavorites();
            e.Handled = true;
        }
    }

    private void OnFavoritePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
            OpenSelectedFavorite();
    }

    private void OpenSelectedFavorite()
    {
        if (FavoritesList.SelectedItem is not FavoriteMenuItem item || ViewModel is null)
            return;

        if (item.IsAddCurrent)
        {
            if (ViewModel.AddCurrentToFavoritesCommand.CanExecute(null))
                ViewModel.AddCurrentToFavoritesCommand.Execute(null);
        }
        else if (item.Favorite is { } favorite &&
                 ViewModel.NavigateToFavoriteCommand.CanExecute(favorite))
        {
            ViewModel.NavigateToFavoriteCommand.Execute(favorite);
        }
        HideFavorites();
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
