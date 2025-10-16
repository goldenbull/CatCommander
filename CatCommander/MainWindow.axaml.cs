using Avalonia.Controls;
using CatCommander.ViewModels;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using Avalonia.Interactivity;
using CatCommander.Services;

namespace CatCommander
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger = LoggingService.CreateLogger<MainWindow>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _logger.LogInformation("DPI: {RenderScaling} {DesktopScaling}", this.RenderScaling, this.DesktopScaling);
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
                return;
            _logger.LogInformation("Button: {btn}", btn);
        }
    }
}