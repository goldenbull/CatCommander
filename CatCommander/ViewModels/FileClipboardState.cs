using System.Collections.Generic;
using CatCommander.Browsing;

namespace CatCommander.ViewModels;

/// <summary>
/// Provider-aware companion to the OS file clipboard. The OS clipboard keeps Finder/Explorer
/// interoperability; this snapshot preserves ResourceRef/provider capabilities for pasting
/// between CatCommander containers, including non-local providers.
/// </summary>
public sealed class FileClipboardState
{
    public IReadOnlyList<BrowserItem> Items { get; private set; } = [];
    public bool MoveOnPaste { get; private set; }
    public ItemBrowserViewModel? SourceTab { get; private set; }

    public void Set(IReadOnlyList<BrowserItem> items, bool moveOnPaste, ItemBrowserViewModel sourceTab)
    {
        Items = [.. items];
        MoveOnPaste = moveOnPaste;
        SourceTab = sourceTab;
    }

    public void ClearIfCurrent(IReadOnlyList<BrowserItem> snapshot)
    {
        if (!ReferenceEquals(Items, snapshot))
            return;

        Items = [];
        MoveOnPaste = false;
        SourceTab = null;
    }
}
