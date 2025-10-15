using Avalonia.Controls;
using CatCommander.ViewModels;

namespace CatCommander.UI;

public partial class ItemsBrowser : UserControl
{
    public ItemsBrowser()
    {
        InitializeComponent();
        DataContext = new ItemsBrowserViewModel();
    }
}
