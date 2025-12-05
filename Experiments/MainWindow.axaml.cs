using Avalonia.Controls;

namespace Experiments;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MyViewModel();
    }
}