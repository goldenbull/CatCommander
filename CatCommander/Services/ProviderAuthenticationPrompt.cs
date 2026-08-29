using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CatCommander.FileSystem;
using CatCommander.Shortcuts;
using CatCommander.View;

namespace CatCommander.Services;

public interface IProviderAuthenticationPrompt
{
    Task<string?> RequestAsync(ProviderAuthenticationChallenge challenge);
}

public sealed class ProviderAuthenticationPrompt : IProviderAuthenticationPrompt
{
    private readonly ShortcutInputContext _inputContext;

    public ProviderAuthenticationPrompt(ShortcutInputContext inputContext)
    {
        _inputContext = inputContext;
    }

    public async Task<string?> RequestAsync(ProviderAuthenticationChallenge challenge)
    {
        var password = new TextBox { PasswordChar = '●', MinWidth = 280 };
        var window = new Window
        {
            Title = challenge.Title,
            Width = 380,
            Height = 170,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16), Spacing = 10,
                Children =
                {
                    new TextBlock { Text = challenge.Prompt },
                    password,
                },
            },
        };
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        var ok = new Button { Content = "Open", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        ok.Click += (_, _) => window.Close(password.Text);
        cancel.Click += (_, _) => window.Close(null);
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        ((StackPanel)window.Content).Children.Add(buttons);
        _inputContext.Track(window, ShortcutScope.Dialog);
        window.InstallEscapeToClose(() => window.Close(null));
        window.InstallEnterSubmits(() => window.Close(password.Text));

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        return owner is null ? null : await window.ShowDialog<string?>(owner);
    }
}
