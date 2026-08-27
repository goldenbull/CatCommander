using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics.CodeAnalysis;
using CatCommander.Platform;

namespace CatCommander.Utils;

/// <summary>
/// Fetches the OS-native icon for a file/folder, encoded as PNG bytes - deliberately not an
/// Avalonia Bitmap. See the design discussion this was built from: the underlying platform calls
/// already produce a raw, framework-agnostic image (a TIFF blob on macOS, a device-independent
/// pixel buffer on Windows) before any UI-framework wrapping happens, so stopping at byte[] costs
/// nothing here and keeps libcat clear of Avalonia (and insulated from Avalonia's own breaking
/// changes across major versions, e.g. Avalonia 11 -> 12).
/// </summary>
public static class SystemIconProvider
{
    private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

    /// <param name="fullPath">Must be a real, existing path - see callers (IconCache) for why.</param>
    /// <param name="size">
    /// Honored on Windows (SHGFI_SMALLICON/LARGEICON). Currently *not* honored on macOS:
    /// NSImage's -setSize: only changes how AppKit displays the image, not the resolution its
    /// TIFFRepresentation encodes at - modern system icons ship multi-resolution assets and this
    /// returns whichever one TIFFRepresentation picks (in practice, often ~1024x1024). Getting an
    /// actually-downscaled bitmap requires drawing into a sized NSImage via lockFocus +
    /// drawInRect:fromRect:..., which takes NSRect structs by value over the Objective-C runtime
    /// bridge - the same category of struct-marshaling hazard that caused the objc_msgSend_stret
    /// bug this file just had (see git history / NOTICE), so deferred rather than risked without
    /// a tight verification loop. Callers get a correct, just larger-than-requested, image;
    /// Avalonia's Image control downscales fine for on-screen display.
    /// </param>
    [UnconditionalSuppressMessage("Interoperability", "CA1416", Justification = "PlatformInfo gates each platform-only call.")]
    public static byte[]? GetIconBytes(string fullPath, bool isDirectory, int size = 32)
    {
        if (string.IsNullOrEmpty(fullPath))
            return null;

        try
        {
            if (PlatformInfo.Current.IsMacOS)
                return GetMacOSIconBytes(fullPath, size);
            if (PlatformInfo.Current.IsWindows)
                return GetWindowsIconBytes(fullPath, isDirectory, size);
            if (PlatformInfo.Current.IsLinux)
                return GetLinuxIconBytes(fullPath, isDirectory, size);

            return null;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to get icon for: {0}", fullPath);
            return null;
        }
    }

    #region macOS

    [SupportedOSPlatform("macos")]
    private static byte[]? GetMacOSIconBytes(string fullPath, int size)
    {
        IntPtr autoreleasePool = IntPtr.Zero;

        try
        {
            MacOSFrameworkLoader.EnsureAppKitLoaded();

            autoreleasePool = objc_msgSend(objc_getClass("NSAutoreleasePool"), sel_registerName("alloc"));
            autoreleasePool = objc_msgSend(autoreleasePool, sel_registerName("init"));

            var workspace = objc_msgSend(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
            var pathString = CreateNSString(fullPath);
            var icon = objc_msgSend(workspace, sel_registerName("iconForFile:"), pathString);
            if (icon == IntPtr.Zero)
                return null;

            // Plain objc_msgSend, not objc_msgSend_stret: -setSize: returns void, it doesn't
            // return a struct - stret is only for struct *return values*. Using it here (as the
            // code this was ported from did) is wrong on any architecture, and fatal specifically
            // on Apple Silicon: objc_msgSend_stret doesn't exist as a separate arm64 entry point
            // at all (the ABI unified struct returns into regular objc_msgSend there), so this
            // used to throw EntryPointNotFoundException on every Apple Silicon Mac.
            var iconSize = new NSSize { width = size, height = size };
            objc_msgSend(icon, sel_registerName("setSize:"), iconSize);

            var tiffData = objc_msgSend(icon, sel_registerName("TIFFRepresentation"));
            if (tiffData == IntPtr.Zero)
                return null;

            // Convert via NSBitmapImageRep rather than decoding the TIFF ourselves - no managed
            // TIFF decoder is available without either System.Drawing.Common (Windows-only since
            // .NET 6) or an Avalonia dependency, and Cocoa already knows how to do this natively.
            var bitmapRepClass = objc_getClass("NSBitmapImageRep");
            var bitmapRep = objc_msgSend(bitmapRepClass, sel_registerName("imageRepWithData:"), tiffData);
            if (bitmapRep == IntPtr.Zero)
                return null;

            var pngData = objc_msgSend_IntPtr_IntPtr(
                bitmapRep,
                sel_registerName("representationUsingType:properties:"),
                (IntPtr)NSBitmapImageFileTypePng,
                IntPtr.Zero);
            if (pngData == IntPtr.Zero)
                return null;

            var length = (int)objc_msgSend(pngData, sel_registerName("length"));
            var bytesPtr = objc_msgSend(pngData, sel_registerName("bytes"));
            if (bytesPtr == IntPtr.Zero || length == 0)
                return null;

            var data = new byte[length];
            Marshal.Copy(bytesPtr, data, 0, length);
            return data;
        }
        finally
        {
            if (autoreleasePool != IntPtr.Zero)
                objc_msgSend(autoreleasePool, sel_registerName("release"));
        }
    }

    private static IntPtr CreateNSString(string str)
    {
        var nsString = objc_msgSend(objc_getClass("NSString"), sel_registerName("alloc"));
        return objc_msgSend(nsString, sel_registerName("initWithUTF8String:"), str);
    }

    // NSBitmapImageFileType.NSBitmapImageFileTypePNG - stable across macOS versions.
    private const long NSBitmapImageFileTypePng = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct NSSize
    {
        public double width;
        public double height;
    }

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, NSSize size);

    #endregion

    #region Windows

    [SupportedOSPlatform("windows")]
    private static byte[]? GetWindowsIconBytes(string fullPath, bool isDirectory, int size)
    {
        var shinfo = new SHFILEINFO();
        var flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (size <= 16 ? SHGFI_SMALLICON : SHGFI_LARGEICON);
        var attributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

        var result = SHGetFileInfo(fullPath, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
        if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            return null;

        try
        {
            return ConvertHIconToPngBytes(shinfo.hIcon);
        }
        finally
        {
            DestroyIcon(shinfo.hIcon);
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[]? ConvertHIconToPngBytes(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out var iconInfo))
            return null;

        try
        {
            GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), out var bmp);
            var width = bmp.bmWidth;
            var height = bmp.bmHeight;

            var screenDC = GetDC(IntPtr.Zero);
            var memDC = CreateCompatibleDC(screenDC);
            var hBitmap = CreateCompatibleBitmap(screenDC, width, height);
            var oldBitmap = SelectObject(memDC, hBitmap);

            DrawIconEx(memDC, 0, 0, hIcon, width, height, 0, IntPtr.Zero, DI_NORMAL);

            var bitmapInfo = new BITMAPINFO
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
            };

            var pixels = new byte[width * height * 4];
            GetDIBits(memDC, hBitmap, 0, (uint)height, pixels, ref bitmapInfo, DIB_RGB_COLORS);

            SelectObject(memDC, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);

            // BGRA -> RGBA
            for (var i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

            // System.Drawing.Common is Windows-only since .NET 6 - fine here, this whole method
            // only ever runs on Windows - but that's exactly why it can't be reused for the macOS
            // path above.
            using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bmpData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            bitmap.UnlockBits(bmpData);

            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero)
                DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != IntPtr.Zero)
                DeleteObject(iconInfo.hbmMask);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO pIconInfo);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int nSize, out BITMAP bmp);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint DI_NORMAL = 0x0003;
    private const uint DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    #endregion

    #region Linux

    private static byte[]? GetLinuxIconBytes(string fullPath, bool isDirectory, int size)
    {
        // Not implemented - freedesktop icon theme lookup is a separate chunk of work, out of
        // scope for this round. UI callers treat null as "no icon", not an error.
        log.Debug("Linux icon retrieval not yet implemented for: {0}", fullPath);
        return null;
    }

    #endregion
}
