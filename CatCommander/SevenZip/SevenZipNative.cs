using System;
using System.Runtime.InteropServices;

namespace CatCommander.SevenZip
{
    /// <summary>
    /// P/Invoke wrapper for the 7-Zip C API
    /// </summary>
    public static class SevenZipNative
    {
        private const string DllName = "7z";

        #region Return Codes

        public const int SZ_OK = 0;
        public const int SZ_ERROR_FAIL = 1;
        public const int SZ_ERROR_MEM = 2;
        public const int SZ_ERROR_UNSUPPORTED = 3;
        public const int SZ_ERROR_PARAM = 4;
        public const int SZ_ERROR_DATA = 5;
        public const int SZ_ERROR_CRC = 6;
        public const int SZ_ERROR_PASSWORD = 7;
        public const int SZ_ERROR_THREAD = 8;

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct SzArchiveInfo
        {
            public uint NumItems;
            public ulong TotalUnpackSize;
            public ulong TotalPackSize;
            public int IsEncrypted;
            public int IsSolid;
            public int NumBlocks;
            public IntPtr FormatName; // const wchar_t*
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SzItemInfo
        {
            public uint Index;
            public IntPtr Path; // const wchar_t*
            public ulong Size;
            public ulong PackedSize;
            public uint Crc;
            public int IsDir;
            public int IsEncrypted;
            public ulong MTime; // Windows FILETIME format
            public ulong CTime;
            public ulong ATime;
            public uint Attributes;
        }

        #endregion

        #region Delegates

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ProgressCallback(IntPtr userData, ulong total, ulong completed);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ExtractCallback(IntPtr userData, uint index,
            [MarshalAs(UnmanagedType.LPWStr)] string path, ulong size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int PasswordCallback(IntPtr userData,
            [MarshalAs(UnmanagedType.LPWStr)] string passwordBuf, int passwordBufSize);

        #endregion

        #region Native Methods

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Sz7z_OpenArchive(
            [MarshalAs(UnmanagedType.LPWStr)] string filePath,
            out IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Sz7z_CloseArchive(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Sz7z_GetArchiveInfo(
            IntPtr handle,
            out SzArchiveInfo info);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Sz7z_GetItemInfo(
            IntPtr handle,
            uint index,
            out SzItemInfo info);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Sz7z_ExtractItem(
            IntPtr handle,
            uint index,
            [MarshalAs(UnmanagedType.LPWStr)] string outputPath,
            ProgressCallback? progressCallback,
            IntPtr userData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Sz7z_ExtractAll(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPWStr)] string outputDir,
            ProgressCallback? progressCallback,
            IntPtr userData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Sz7z_TestArchive(
            IntPtr handle,
            ProgressCallback? progressCallback,
            IntPtr userData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Sz7z_SetPassword(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPWStr)] string password);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Sz7z_GetLastError(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder buffer,
            int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Sz7z_GetVersion(out uint major, out uint minor);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Sz7z_GetSupportedFormats(
            [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder buffer,
            int bufferSize);

        #endregion
    }
}
