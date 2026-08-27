using Avalonia.Data.Converters;

namespace CatCommander.Converters;

/// <summary>
/// Small XAML-referenceable boolean converters that don't warrant a one-off FuncValueConverter
/// field wherever they're needed - e.g. ItemBrowser.axaml's Classes.inactive binding, which needs
/// the logical negation of MainPanelViewModel.IsActive.
/// </summary>
public static class BooleanConverters
{
    public static readonly IValueConverter Not = new FuncValueConverter<bool, bool>(b => !b);
}
