using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CatCommander.View;

/// <summary>
/// Result of a MessageBox dialog
/// </summary>
public enum MessageBoxResult
{
    None = 0,
    OK = 1,
    Cancel = 2,
    Yes = 6,
    No = 7
}

/// <summary>
/// Buttons to display in a MessageBox
/// </summary>
public enum MessageBoxButton
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel
}

/// <summary>
/// Icon to display in a MessageBox
/// </summary>
public enum MessageBoxImage
{
    None,
    Information,
    Question,
    Warning,
    Error
}

/// <summary>
/// Utility class for displaying message boxes, similar to WPF's MessageBox
/// </summary>
public static class MessageBox
{
    /// <summary>
    /// Shows a message box with the specified text
    /// </summary>
    public static Task<MessageBoxResult> Show(string messageBoxText)
    {
        return Show(null, messageBoxText, "CatCommander", MessageBoxButton.OK, MessageBoxImage.None);
    }

    /// <summary>
    /// Shows a message box with the specified text and title
    /// </summary>
    public static Task<MessageBoxResult> Show(string messageBoxText, string caption)
    {
        return Show(null, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);
    }

    /// <summary>
    /// Shows a message box with the specified text, title, and button
    /// </summary>
    public static Task<MessageBoxResult> Show(string messageBoxText, string caption, MessageBoxButton button)
    {
        return Show(null, messageBoxText, caption, button, MessageBoxImage.None);
    }

    /// <summary>
    /// Shows a message box with the specified text, title, button, and icon
    /// </summary>
    public static Task<MessageBoxResult> Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
    {
        return Show(null, messageBoxText, caption, button, icon);
    }

    /// <summary>
    /// Shows a message box with the specified owner, text, title, button, and icon
    /// </summary>
    public static async Task<MessageBoxResult> Show(
        Window? owner,
        string messageBoxText,
        string caption = "CatCommander",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        var dialog = new Window
        {
            Title = caption,
            Width = 400,
            MinWidth = 300,
            MaxWidth = 600,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            CanResize = false,
            ShowInTaskbar = false
        };

        MessageBoxResult result = MessageBoxResult.None;
        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20
        };

        // Content panel with icon and message
        var contentPanel = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 10)
        };

        // Add icon if specified
        if (icon != MessageBoxImage.None)
        {
            var iconText = new TextBlock
            {
                Text = GetIconSymbol(icon),
                FontSize = 32,
                Foreground = GetIconColor(icon),
                Margin = new Thickness(0, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            DockPanel.SetDock(iconText, Avalonia.Controls.Dock.Left);
            contentPanel.Children.Add(iconText);
        }

        // Message text
        var messageText = new TextBlock
        {
            Text = messageBoxText,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        contentPanel.Children.Add(messageText);

        mainPanel.Children.Add(contentPanel);

        // Button panel
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        // Add buttons based on MessageBoxButton parameter
        switch (button)
        {
            case MessageBoxButton.OK:
                AddButton(buttonPanel, "OK", MessageBoxResult.OK, true, dialog, r => result = r);
                break;

            case MessageBoxButton.OKCancel:
                AddButton(buttonPanel, "OK", MessageBoxResult.OK, true, dialog, r => result = r);
                AddButton(buttonPanel, "Cancel", MessageBoxResult.Cancel, false, dialog, r => result = r);
                break;

            case MessageBoxButton.YesNo:
                AddButton(buttonPanel, "Yes", MessageBoxResult.Yes, false, dialog, r => result = r);
                AddButton(buttonPanel, "No", MessageBoxResult.No, true, dialog, r => result = r);
                break;

            case MessageBoxButton.YesNoCancel:
                AddButton(buttonPanel, "Yes", MessageBoxResult.Yes, false, dialog, r => result = r);
                AddButton(buttonPanel, "No", MessageBoxResult.No, true, dialog, r => result = r);
                AddButton(buttonPanel, "Cancel", MessageBoxResult.Cancel, false, dialog, r => result = r);
                break;
        }

        mainPanel.Children.Add(buttonPanel);
        dialog.Content = mainPanel;

        if (owner != null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
            var tcs = new TaskCompletionSource<bool>();
            dialog.Closed += (s, e) => tcs.TrySetResult(true);
            await tcs.Task;
        }

        return result;
    }

    private static void AddButton(
        StackPanel panel,
        string content,
        MessageBoxResult dialogResult,
        bool isDefault,
        Window dialog,
        Action<MessageBoxResult> setResult)
    {
        var button = new Button
        {
            Content = content,
            Width = 80,
            Height = 32,
            IsDefault = isDefault
        };

        button.Click += (s, e) =>
        {
            setResult(dialogResult);
            dialog.Close();
        };

        panel.Children.Add(button);
    }

    private static string GetIconSymbol(MessageBoxImage icon)
    {
        return icon switch
        {
            MessageBoxImage.Information => "ℹ️",
            MessageBoxImage.Question => "❓",
            MessageBoxImage.Warning => "⚠️",
            MessageBoxImage.Error => "❌",
            _ => ""
        };
    }

    private static IBrush GetIconColor(MessageBoxImage icon)
    {
        return icon switch
        {
            MessageBoxImage.Information => Brushes.DodgerBlue,
            MessageBoxImage.Question => Brushes.Green,
            MessageBoxImage.Warning => Brushes.Orange,
            MessageBoxImage.Error => Brushes.Red,
            _ => Brushes.Black
        };
    }
}
