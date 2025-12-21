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
                // register SharpHook
                var keyboardHook = KeyboardHookManager.Instance;
                keyboardHook.KeyPressed += OnGlobalKeyPressed;
                keyboardHook.Start();
                log.Info("Keyboard hook initialized and started");

                // and also register PreviewKeyDown
                AddHandler(KeyDownEvent, Window_PreviewKeyDown, RoutingStrategies.Tunnel, true);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to initialize keyboard hook");
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

        private void Window_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            var keyInfo = $"Window_PreviewKeyDown {e.KeyModifiers} {e.Key}";

            // tbKeyPreview.Text = keyInfo;
            log.Debug(keyInfo);
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