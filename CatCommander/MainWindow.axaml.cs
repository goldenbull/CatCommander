using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
            DataContext = new MainViewModel();
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
                return;
            log.Debug($"Button: {btn.Name}");
        }
    }
}