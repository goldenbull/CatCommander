using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace CatCommander.FileSystem;

public interface ITrashService
{
    void MoveToTrash(string path);
}

/// <summary>Moves local resources to the OS-managed trash without a permanent-delete fallback.</summary>
public sealed class SystemTrashService : ITrashService
{
    public static SystemTrashService Instance { get; } = new();

    private SystemTrashService()
    {
    }

    public void MoveToTrash(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            MoveToWindowsRecycleBin(path);
            return;
        }
        if (OperatingSystem.IsMacOS())
        {
            MacTrash.Move(path);
            return;
        }

        throw new PlatformNotSupportedException(
            "Moving files to trash is currently supported on macOS and Windows.");
    }

    private static void MoveToWindowsRecycleBin(string path)
    {
        if (Directory.Exists(path))
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        }
        else if (File.Exists(path))
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        }
    }

    private static class MacTrash
    {
        private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";

        public static void Move(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                return;

            var pool = Send(GetClass("NSAutoreleasePool"), Selector("new"));
            try
            {
                var nsPath = SendString(GetClass("NSString"), Selector("stringWithUTF8String:"), path);
                var url = SendObject(GetClass("NSURL"), Selector("fileURLWithPath:"), nsPath);
                var manager = Send(GetClass("NSFileManager"), Selector("defaultManager"));
                if (!SendTrash(manager, Selector("trashItemAtURL:resultingItemURL:error:"), url, IntPtr.Zero, IntPtr.Zero))
                    throw new IOException($"macOS failed to move '{path}' to Trash.");
            }
            finally
            {
                SendVoid(pool, Selector("drain"));
            }
        }

        [DllImport(ObjCLibrary, EntryPoint = "objc_getClass")]
        private static extern IntPtr GetClass(string name);

        [DllImport(ObjCLibrary, EntryPoint = "sel_registerName")]
        private static extern IntPtr Selector(string name);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendString(IntPtr receiver, IntPtr selector, string value);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendObject(IntPtr receiver, IntPtr selector, IntPtr value);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SendTrash(
            IntPtr receiver, IntPtr selector, IntPtr url, IntPtr resultingUrl, IntPtr error);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern void SendVoid(IntPtr receiver, IntPtr selector);
    }
}
