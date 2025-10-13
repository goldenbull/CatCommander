using System;
using Avalonia.Controls;

namespace CatCommander
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public bool IsWindowsOS => OperatingSystem.IsWindows();
    }
}