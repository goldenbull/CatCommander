using System.Runtime.InteropServices;

namespace CatCommander.Utils;

/// <summary>
/// Ensures AppKit.framework is loaded before any Objective-C runtime call touches AppKit classes
/// (NSWorkspace, NSBitmapImageRep, ...). A bare console/CLI-hosted .NET process on macOS does not
/// have AppKit loaded by default - only a real windowed app does, via its own native windowing
/// backend - so without this, objc_getClass("NSWorkspace") silently returns null (not an
/// exception) and every message sent to it also silently returns null, which is easy to
/// misdiagnose as "correctly returned a negative/empty result". Idempotent: dlopen on an
/// already-loaded framework just returns the existing handle, so calling this defensively from
/// every AppKit-touching call site costs nothing.
/// </summary>
internal static class MacOSFrameworkLoader
{
    private static readonly IntPtr AppKitHandle = dlopen(
        "/System/Library/Frameworks/AppKit.framework/AppKit", RTLD_NOW);

    public static void EnsureAppKitLoaded()
    {
        _ = AppKitHandle; // referencing the field is enough to force the static ctor to run
    }

    private const int RTLD_NOW = 2;

    [DllImport("libdl.dylib")]
    private static extern IntPtr dlopen(string path, int mode);
}
