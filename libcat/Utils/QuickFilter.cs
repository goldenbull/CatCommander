namespace CatCommander.Utils;

/// <summary>
/// Total Commander-style "quick filter" matching for a single item name against typed filter
/// text: the filter text is split on spaces into tokens, and a name matches only if it contains
/// every token (AND, not OR) - "aa bb" matches "aaccbb" but neither "aacc" nor "bbcc" alone.
/// Case-insensitive, ordinal (file names aren't culture-sensitive text).
/// </summary>
public static class QuickFilter
{
    public static bool Matches(string filterText, string name)
    {
        if (string.IsNullOrWhiteSpace(filterText))
            return true;

        foreach (var token in filterText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!name.Contains(token, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
