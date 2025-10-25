using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NLog;

namespace CatCommander.Utils;

/// <summary>
/// Provides system icons for files and folders across different operating systems
/// Returns Avalonia Bitmap objects
/// </summary>
public static class SystemIconProvider
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Gets the system icon for a file or folder as an Avalonia Bitmap
    /// </summary>
    /// <param name="fullPath">Full path to the file or folder</param>
    /// <param name="size">Desired icon size (default: 32)</param>
    /// <returns>Bitmap of the icon or null if unavailable</returns>
    public static Bitmap? GetIcon(string fullPath, int size = 32)
    {
        if (string.IsNullOrEmpty(fullPath))
            return null;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacOSIcon(fullPath, size);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsIcon(fullPath, size);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetLinuxIcon(fullPath, size);
            else
                return null;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get icon for: {fullPath}");
            return null;
        }
    }

    #region macOS Implementation

    private static Bitmap? GetMacOSIcon(string fullPath, int size)
    {
        try
        {
            // Get NSImage data using Objective-C runtime
            var imageData = GetMacOSIconData(fullPath, size);
            if (imageData == null || imageData.Length == 0)
                return null;

            // Convert to Avalonia Bitmap
            using var memoryStream = new MemoryStream(imageData);
            return new Bitmap(memoryStream);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get macOS icon for: {fullPath}");
            return null;
        }
    }

    private static byte[]? GetMacOSIconData(string fullPath, int size)
    {
        IntPtr autoreleasePool = IntPtr.Zero;
        IntPtr workspace = IntPtr.Zero;
        IntPtr icon = IntPtr.Zero;
        IntPtr tiffData = IntPtr.Zero;

        try
        {
            // Create autorelease pool
            autoreleasePool = objc_msgSend(objc_getClass("NSAutoreleasePool"), sel_registerName("alloc"));
            autoreleasePool = objc_msgSend(autoreleasePool, sel_registerName("init"));

            // Get shared workspace
            workspace = objc_msgSend(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));

            // Create NSString for the path
            IntPtr pathString = CreateNSString(fullPath);

            // Get icon for file
            icon = objc_msgSend(workspace, sel_registerName("iconForFile:"), pathString);

            if (icon == IntPtr.Zero)
                return null;

            // Set icon size
            var iconSize = new NSSize { width = size, height = size };
            objc_msgSend_stret(out iconSize, icon, sel_registerName("setSize:"), iconSize);

            // Get TIFF representation
            tiffData = objc_msgSend(icon, sel_registerName("TIFFRepresentation"));

            if (tiffData == IntPtr.Zero)
                return null;

            // Get data length and bytes
            var length = (int)objc_msgSend(tiffData, sel_registerName("length"));
            var bytes = objc_msgSend(tiffData, sel_registerName("bytes"));

            if (bytes == IntPtr.Zero || length == 0)
                return null;

            // Copy to managed byte array
            byte[] data = new byte[length];
            Marshal.Copy(bytes, data, 0, length);

            // Convert TIFF to PNG for better compatibility
            return ConvertTiffToPng(data);
        }
        finally
        {
            // Release autorelease pool
            if (autoreleasePool != IntPtr.Zero)
                objc_msgSend(autoreleasePool, sel_registerName("release"));
        }
    }

    private static byte[]? ConvertTiffToPng(byte[] tiffData)
    {
        try
        {
            // Load TIFF and save as PNG
            using var tiffStream = new MemoryStream(tiffData);
            using var bitmap = new Bitmap(tiffStream);
            using var pngStream = new MemoryStream();
            bitmap.Save(pngStream);
            return pngStream.ToArray();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to convert TIFF to PNG");
            return tiffData; // Return original TIFF data as fallback
        }
    }

    private static IntPtr CreateNSString(string str)
    {
        var nsString = objc_msgSend(objc_getClass("NSString"), sel_registerName("alloc"));
        return objc_msgSend(nsString, sel_registerName("initWithUTF8String:"), str);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NSSize
    {
        public double width;
        public double height;
    }

    // Objective-C runtime P/Invoke declarations
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

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, NSSize size);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern void objc_msgSend_stret(out NSSize retval, IntPtr receiver, IntPtr selector, NSSize size);

    #endregion

    #region Windows Implementation

    [SupportedOSPlatform("windows")]
    private static Bitmap? GetWindowsIcon(string fullPath, int size)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        try
        {
            var shinfo = new SHFILEINFO();
            var flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;

            // Determine size flag
            if (size <= 16)
                flags |= SHGFI_SMALLICON;
            else if (size <= 32)
                flags |= SHGFI_LARGEICON;
            else
                flags |= SHGFI_LARGEICON; // Use large and scale

            var result = SHGetFileInfo(
                fullPath,
                FILE_ATTRIBUTE_NORMAL,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                flags);

            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                // Convert HICON to Bitmap
                return ConvertHIconToBitmap(shinfo.hIcon);
            }
            finally
            {
                // Clean up icon handle
                DestroyIcon(shinfo.hIcon);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get Windows icon for: {fullPath}");
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Bitmap? ConvertHIconToBitmap(IntPtr hIcon)
    {
        try
        {
            // Get icon info
            ICONINFO iconInfo;
            if (!GetIconInfo(hIcon, out iconInfo))
                return null;

            try
            {
                // Get bitmap info
                BITMAP bmp;
                GetObject(iconInfo.hbmColor, Marshal.SizeOf(typeof(BITMAP)), out bmp);

                var width = bmp.bmWidth;
                var height = bmp.bmHeight;

                // Create device context
                var screenDC = GetDC(IntPtr.Zero);
                var memDC = CreateCompatibleDC(screenDC);
                var hBitmap = CreateCompatibleBitmap(screenDC, width, height);
                var oldBitmap = SelectObject(memDC, hBitmap);

                // Draw icon
                DrawIconEx(memDC, 0, 0, hIcon, width, height, 0, IntPtr.Zero, DI_NORMAL);

                // Get bitmap bits
                var bitmapInfo = new BITMAPINFO();
                bitmapInfo.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bitmapInfo.biWidth = width;
                bitmapInfo.biHeight = -height; // Top-down
                bitmapInfo.biPlanes = 1;
                bitmapInfo.biBitCount = 32;
                bitmapInfo.biCompression = BI_RGB;

                var pixelCount = width * height;
                var pixels = new byte[pixelCount * 4];

                GetDIBits(memDC, hBitmap, 0, (uint)height, pixels, ref bitmapInfo, DIB_RGB_COLORS);

                // Clean up
                SelectObject(memDC, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);

                // Convert BGRA to RGBA and create bitmap
                return CreateBitmapFromPixels(pixels, width, height);
            }
            finally
            {
                if (iconInfo.hbmColor != IntPtr.Zero)
                    DeleteObject(iconInfo.hbmColor);
                if (iconInfo.hbmMask != IntPtr.Zero)
                    DeleteObject(iconInfo.hbmMask);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to convert HICON to Bitmap");
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Bitmap? CreateBitmapFromPixels(byte[] pixels, int width, int height)
    {
        try
        {
            // Convert BGRA to RGBA
            for (int i = 0; i < pixels.Length; i += 4)
            {
                var b = pixels[i];
                var r = pixels[i + 2];
                pixels[i] = r;
                pixels[i + 2] = b;
            }

            // Create Avalonia bitmap from pixel data
            using var stream = new MemoryStream();
            using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var bmpData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            bitmap.UnlockBits(bmpData);

            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;

            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to create bitmap from pixels");
            return null;
        }
    }

    // Windows API declarations
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

    // Windows constants
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
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

    #region Linux Implementation

    private static Bitmap? GetLinuxIcon(string fullPath, int size)
    {
        // TODO: Implement using GTK/Qt icon themes or file associations
        // For now, return null - can be implemented using process calls to gio or similar
        log.Debug($"Linux icon retrieval not yet implemented for: {fullPath}");
        return null;
    }

    #endregion
}
