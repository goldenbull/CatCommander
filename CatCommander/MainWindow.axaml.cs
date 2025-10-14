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
    }
}