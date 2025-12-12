using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.Commands;
using CatCommander.Configuration;
using CatCommander.Utils;
using CatCommander.View;
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

            // Initialize keyboard hook manager
            InitializeKeyboardHook();

            // Handle Closing event to detect Meta+Q triggered closes
            Closing += MainWindow_Closing;
        }

        private void InitializeKeyboardHook()
        {
            try
            {
                var keyboardHook = KeyboardHookManager.Instance;
                keyboardHook.KeyPressed += OnGlobalKeyPressed;
                keyboardHook.Start();
                log.Info("Keyboard hook initialized and started");
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to initialize keyboard hook. Falling back to Avalonia events.");
                // Fallback to Avalonia's keyboard events if hook fails
                AddHandler(KeyDownEvent, Window_PreviewKeyDown,
                    RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
                AddHandler(KeyUpEvent, Window_PreviewKeyUp, RoutingStrategies.Tunnel, true);
            }
        }

        private void OnGlobalKeyPressed(object? sender, CatKeyEventArgs e)
        {
            // Run on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnSharpHookKeyPressed(e));
        }

        void OnSharpHookKeyPressed(CatKeyEventArgs e)
        {
            var keyCombination = e.ToString();
            tbKeyPreview.Text = keyCombination;
            var op = ConfigManager.Instance.Shortcuts.GetOperation(e);
            if (op != Operation.Nop)
            {
                log.Debug($"OnSharpHookKeyPressed: {keyCombination} {op}");
                // execute operation
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            log.Debug($"OnKeyDown: {e.KeyModifiers} {e.Key} {e.KeySymbol} {e.PhysicalKey} ");
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
                // User confirmed exit, cleanup and close
                KeyboardHookManager.Instance.Dispose();
                Closing -= MainWindow_Closing;
                Close();
            }
        }
    }
}