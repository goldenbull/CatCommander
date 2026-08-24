using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CatCommander.QuickAccess;

namespace CatCommander.Converters;

/// <summary>
/// Maps QuickAccessKind to a text glyph for the quick access row. QuickAccessKind stays plain
/// data in libcat (no Bitmap) - this is the "View layer maps Kind to an actual icon" piece the
/// design calls for, kept to a text glyph rather than a bitmap asset pipeline for now.
/// </summary>
public class QuickAccessKindToGlyphConverter : IValueConverter
{
    public static readonly QuickAccessKindToGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            QuickAccessKind.Drive => "\U0001F5B4",
            QuickAccessKind.Removable => "\U0001F50C",
            QuickAccessKind.Network => "\U0001F310",
            QuickAccessKind.Optical => "\U0001F4BF",
            QuickAccessKind.SpecialFolder => "\U0001F4C1",
            _ => "\U0001F4C1",
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
