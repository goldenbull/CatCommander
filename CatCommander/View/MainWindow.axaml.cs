using Avalonia.Controls;
using Avalonia.Input;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel, ShortcutsSettings shortcuts)
    {
        InitializeComponent();
        DataContext = viewModel;
        ShortcutRouter.Install(this, shortcuts);

        // The native macOS menu bar's keyEquivalents are plain property sets, not something that
        // re-reads ShortcutsSettings live the way ShortcutRouter's dispatch does - set once here
        // from the same effective bindings ShortcutRouter would use, instead of a hardcoded XAML
        // Gesture= string (see ShortcutsSettings.GetPrimaryGesture's own doc comment for why that
        // used to be able to drift from what a real keystroke actually dispatched).
        ApplyMenuGestures(shortcuts);
        viewModel.ShortcutsChanged += () => ApplyMenuGestures(shortcuts);

        // LeftPanel/RightPanel's ItemBrowsers each already self-focus the moment they're attached
        // (see ItemBrowser.axaml.cs), but that race is decided purely by visual-tree construction
        // order, not by which panel the ViewModel actually considers active. Re-asserting it once
        // more here, after everything's attached and shown, makes the two agree deterministically.
        Opened += (_, _) => viewModel.ActivePanel?.RequestFocus();
    }

    // x:Name on a NativeMenuItem doesn't generate a code-behind field the way it does for a real
    // Visual - NativeMenu/NativeMenuItem live in a separate object graph (platform menu
    // abstractions, not the visual tree), which the XAML compiler's field generation doesn't walk.
    // Looked up by Header text instead - each is unique within MainWindow.axaml's menu.
    private void ApplyMenuGestures(ShortcutsSettings shortcuts)
    {
        if (NativeMenu.GetMenu(this) is not { } menu)
            return;

        SetGesture(menu, "Copy", shortcuts.GetPrimaryGesture(Operation.Copy));
        SetGesture(menu, "Move", shortcuts.GetPrimaryGesture(Operation.Move));
        SetGesture(menu, "Rename", shortcuts.GetPrimaryGesture(Operation.Rename));
        SetGesture(menu, "Delete", shortcuts.GetPrimaryGesture(Operation.Delete));
        SetGesture(menu, "New Folder", shortcuts.GetPrimaryGesture(Operation.CreateDirectory));
        SetGesture(menu, "Find...", shortcuts.GetPrimaryGesture(Operation.OpenFind));
    }

    private static bool SetGesture(NativeMenu menu, string header, KeyGesture? gesture)
    {
        foreach (var item in menu.Items)
        {
            if (item is not NativeMenuItem menuItem)
                continue;

            if (menuItem.Header == header)
            {
                menuItem.Gesture = gesture;
                return true;
            }

            if (menuItem.Menu is { } submenu && SetGesture(submenu, header, gesture))
                return true;
        }

        return false;
    }
}
