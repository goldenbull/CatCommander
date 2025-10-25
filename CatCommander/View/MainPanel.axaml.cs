using Avalonia.Controls;
using CatCommander.ViewModels;

namespace CatCommander.View;

public partial class MainPanel : UserControl
{
    public MainPanel()
    {
        InitializeComponent();
        DataContext = new MainPanelViewModel();
    }
}