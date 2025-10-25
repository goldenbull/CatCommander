using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.Utils;
using CatCommander.ViewModels;
using NLog;

namespace CatCommander
{
    public partial class MainWindow : Window
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private DateTime? lastMetaKeyPress;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();

            // Attach PreviewKeyDown event handler (tunneling event)
            // handledEventsToo=true ensures we get events even if they're marked as handled
            // Use Tunnel | Bubble to catch events in both phases
            AddHandler(KeyDownEvent, Window_PreviewKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);

            // Also listen to KeyUp to detect Meta key releases
            AddHandler(KeyUpEvent, Window_PreviewKeyUp, RoutingStrategies.Tunnel, true);

            // Handle Closing event to detect Meta+Q triggered closes
            Closing += MainWindow_Closing;

            // Add KeyBinding for Control+Tab to intercept it before other controls
            this.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.Tab, KeyModifiers.Control),
                Command = ReactiveUI.ReactiveCommand.Create(() =>
                {
                    var keyInfo = "Control Tab (CAPTURED via KeyBinding!)";
                    tbKeyPreview.Text = keyInfo;
                    log.Debug(keyInfo);
                })
            });

            // Override the Quit command to intercept Meta+Q
            this.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.Q, KeyModifiers.Meta),
                Command = ReactiveUI.ReactiveCommand.Create(() =>
                {
                    var keyInfo = "Meta Q (CAPTURED via KeyBinding)";
                    tbKeyPreview.Text = keyInfo;
                    log.Debug(keyInfo);
                })
            });
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
                return;
            log.Debug($"Button: {btn.Name}");
        }

        private void Window_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            var keyInfo = $"{e.KeyModifiers} {e.Key}";

            // Track Meta key presses for Meta+Q detection
            if (e.Key == Key.LWin || e.Key == Key.RWin)
            {
                lastMetaKeyPress = DateTime.Now;
            }

            // Check for Meta+Q combination
            if (e.Key == Key.Q && e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                keyInfo = $"{e.KeyModifiers} {e.Key} (Meta+Q CAPTURED!)";
                e.Handled = true; // Try to prevent default quit behavior
                tbKeyPreview.Text = keyInfo;
                log.Debug(keyInfo);
                return;
            }

            // Special handling for Control+Tab to prevent tab navigation
            // Check both Control and Shift+Control combinations
            if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                keyInfo = $"{e.KeyModifiers} {e.Key} (Ctrl+Tab CAPTURED in PreviewKeyDown!)";
                e.Handled = true; // Prevent default tab navigation
                tbKeyPreview.Text = keyInfo;
                log.Debug(keyInfo);
                return;
            }

            // Also capture plain Tab to see if it's reaching here
            if (e.Key == Key.Tab)
            {
                keyInfo = $"{e.KeyModifiers} {e.Key} (Tab key detected)";
            }

            tbKeyPreview.Text = keyInfo;
            log.Debug(keyInfo);
        }

        private void Window_PreviewKeyUp(object? sender, KeyEventArgs e)
        {
            // Optional: Log key releases for debugging
            if (e.Key == Key.Q && lastMetaKeyPress.HasValue &&
                (DateTime.Now - lastMetaKeyPress.Value).TotalMilliseconds < 1000)
            {
                var keyInfo = "Meta+Q released (detected in KeyUp)";
                log.Debug(keyInfo);
            }
        }

        private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            // This will be called when Meta+Q triggers application quit
            var keyInfo = "Window Closing (possibly Meta+Q)";
            log.Debug(keyInfo);

            // Cancel the close operation first
            e.Cancel = true;

            // Show confirmation dialog using MessageBox utility
            var result = await MessageBox.Show(
                this,
                "Are you sure you want to exit CatCommander?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // User confirmed exit, close without triggering this event again
                Closing -= MainWindow_Closing;
                Close();
            }
        }
    }
}