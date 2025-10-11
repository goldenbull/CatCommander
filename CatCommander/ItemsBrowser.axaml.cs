using Avalonia.Controls;
using CatCommander.ViewModels;

namespace CatCommander;

public partial class ItemsBrowser : UserControl
{
    public ItemsBrowser()
    {
        InitializeComponent();
        DataContext = new ItemsBrowserViewModel();
    }
}
