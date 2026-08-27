using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics.CodeAnalysis;
using CatCommander.Platform;

namespace CatCommander.Utils;

/// <summary>
/// Checks whether this process is the frontmost (focused) application - used to gate
/// GlobalShortcutGuard's low-level input path so it only ever acts while CatCommander is actually
/// the app the user is interacting with, never as a true background-regardless-of-focus hotkey.
/// </summary>
public static class ForegroundAppChecker
{
    [UnconditionalSuppressMessage("Interoperability", "CA1416", Justification = "PlatformInfo gates the macOS-only call.")]
    public static bool IsFrontmostApplication()
    {
        if (PlatformInfo.Current.IsMacOS)
            return IsFrontmostApplicationMacOS();

        // On Windows/Linux the active-window command-source check is the current foreground gate;
        // platform-native foreground-process checks can replace this conservative default later.
        return true;
    }

    [SupportedOSPlatform("macos")]
    private static bool IsFrontmostApplicationMacOS()
    {
        IntPtr autoreleasePool = IntPtr.Zero;

        try
        {
            MacOSFrameworkLoader.EnsureAppKitLoaded();

            autoreleasePool = objc_msgSend(objc_getClass("NSAutoreleasePool"), sel_registerName("alloc"));
            autoreleasePool = objc_msgSend(autoreleasePool, sel_registerName("init"));

            var workspaceClass = objc_getClass("NSWorkspace");
            var sharedWorkspace = objc_msgSend(workspaceClass, sel_registerName("sharedWorkspace"));
            var frontmostApp = objc_msgSend(sharedWorkspace, sel_registerName("frontmostApplication"));

            if (frontmostApp == IntPtr.Zero)
                return false;

            var pid = objc_msgSend_ReturningInt(frontmostApp, sel_registerName("processIdentifier"));
            return pid == Environment.ProcessId;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (autoreleasePool != IntPtr.Zero)
                objc_msgSend(autoreleasePool, sel_registerName("release"));
        }
    }

    // Objective-C runtime P/Invoke declarations (same idiom as SystemIconProvider's macOS icon code).
    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern int objc_msgSend_ReturningInt(IntPtr receiver, IntPtr selector);
}
