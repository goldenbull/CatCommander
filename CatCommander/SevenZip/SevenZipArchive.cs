using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CatCommander.SevenZip
{
    /// <summary>
    /// High-level managed wrapper for 7-Zip operations
    /// </summary>
    public class SevenZipArchive : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        public string FilePath { get; private set; }

        public SevenZipArchive(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

            int result = SevenZipNative.Sz7z_OpenArchive(filePath, out _handle);
            if (result != SevenZipNative.SZ_OK)
            {
                throw new SevenZipException($"Failed to open archive: {GetErrorMessage(result)}", result);
            }
        }

        /// <summary>
        /// Gets information about the archive
        /// </summary>
        public ArchiveInfo GetInfo()
        {
            ThrowIfDisposed();

            int result = SevenZipNative.Sz7z_GetArchiveInfo(_handle, out var info);
            if (result != SevenZipNative.SZ_OK)
            {
                throw new SevenZipException($"Failed to get archive info: {GetErrorMessage(result)}", result);
            }

            return new ArchiveInfo
            {
                NumItems = info.NumItems,
                TotalUnpackSize = info.TotalUnpackSize,
                TotalPackSize = info.TotalPackSize,
                IsEncrypted = info.IsEncrypted != 0,
                IsSolid = info.IsSolid != 0,
                NumBlocks = info.NumBlocks
            };
        }

        /// <summary>
        /// Gets information about a specific item
        /// </summary>
        public ArchiveItem GetItemInfo(uint index)
        {
            ThrowIfDisposed();

            int result = SevenZipNative.Sz7z_GetItemInfo(_handle, index, out var info);
            if (result != SevenZipNative.SZ_OK)
            {
                throw new SevenZipException($"Failed to get item info: {GetErrorMessage(result)}", result);
            }

            return new ArchiveItem
            {
                Index = info.Index,
                Path = Marshal.PtrToStringUni(info.Path) ?? string.Empty,
                Size = info.Size,
                PackedSize = info.PackedSize,
                Crc = info.Crc,
                IsDirectory = info.IsDir != 0,
                IsEncrypted = info.IsEncrypted != 0,
                ModifiedTime = DateTime.FromFileTimeUtc((long)info.MTime),
                CreatedTime = info.CTime != 0 ? DateTime.FromFileTimeUtc((long)info.CTime) : null,
                AccessedTime = info.ATime != 0 ? DateTime.FromFileTimeUtc((long)info.ATime) : null,
                Attributes = info.Attributes
            };
        }

        /// <summary>
        /// Gets all items in the archive
        /// </summary>
        public List<ArchiveItem> GetAllItems()
        {
            var archiveInfo = GetInfo();
            var items = new List<ArchiveItem>((int)archiveInfo.NumItems);

            for (uint i = 0; i < archiveInfo.NumItems; i++)
            {
                items.Add(GetItemInfo(i));
            }

            return items;
        }

        /// <summary>
        /// Extracts a single item to a file
        /// </summary>
        public void ExtractItem(uint index, string outputPath, Action<ulong, ulong>? progressCallback = null)
        {
            ThrowIfDisposed();

            SevenZipNative.ProgressCallback? nativeCallback = null;
            if (progressCallback != null)
            {
                nativeCallback = (userData, total, completed) => progressCallback(total, completed);
            }

            int result = SevenZipNative.Sz7z_ExtractItem(_handle, index, outputPath, nativeCallback, IntPtr.Zero);
            if (result != SevenZipNative.SZ_OK)
            {
                throw new SevenZipException($"Failed to extract item: {GetErrorMessage(result)}", result);
            }
        }

        /// <summary>
        /// Extracts all items to a directory
        /// </summary>
        public void ExtractAll(string outputDir, Action<ulong, ulong>? progressCallback = null)
        {
            ThrowIfDisposed();

            SevenZipNative.ProgressCallback? nativeCallback = null;
            if (progressCallback != null)
            {
                nativeCallback = (userData, total, completed) => progressCallback(total, completed);
            }

            int result = SevenZipNative.Sz7z_ExtractAll(_handle, outputDir, nativeCallback, IntPtr.Zero);
            if (result != SevenZipNative.SZ_OK)
            {
                throw new SevenZipException($"Failed to extract all: {GetErrorMessage(result)}", result);
            }
        }

        /// <summary>
        /// Tests the archive integrity
        /// </summary>
        public void Test(Action<ulong, ulong>? progressCallback = null)
        {
            ThrowIfDisposed();

            SevenZipNative.ProgressCallback? nativeCallback = null;
            if (progressCallback != null)
            {
                nativeCallback = (userData, total, completed) => progressCallback(total, completed);
            }

            int result = SevenZipNative.Sz7z_TestArchive(_handle, nativeCallback, IntPtr.Zero);
            if (result != SevenZipNative.SZ_OK)
            {
                throw new SevenZipException($"Archive test failed: {GetErrorMessage(result)}", result);
            }
        }

        /// <summary>
        /// Sets the password for encrypted archives
        /// </summary>
        public void SetPassword(string password)
        {
            ThrowIfDisposed();

            int result = SevenZipNative.Sz7z_SetPassword(_handle, password);
            if (result != SevenZipNative.SZ_OK)
            {
                throw new SevenZipException($"Failed to set password: {GetErrorMessage(result)}", result);
            }
        }

        /// <summary>
        /// Gets the 7-Zip version
        /// </summary>
        public static Version GetVersion()
        {
            SevenZipNative.Sz7z_GetVersion(out uint major, out uint minor);
            return new Version((int)major, (int)minor);
        }

        /// <summary>
        /// Gets supported archive formats
        /// </summary>
        public static string[] GetSupportedFormats()
        {
            var buffer = new System.Text.StringBuilder(1024);
            SevenZipNative.Sz7z_GetSupportedFormats(buffer, buffer.Capacity);
            return buffer.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        }

        private string GetErrorMessage(int errorCode)
        {
            return errorCode switch
            {
                SevenZipNative.SZ_ERROR_FAIL => "Operation failed",
                SevenZipNative.SZ_ERROR_MEM => "Out of memory",
                SevenZipNative.SZ_ERROR_UNSUPPORTED => "Unsupported operation",
                SevenZipNative.SZ_ERROR_PARAM => "Invalid parameter",
                SevenZipNative.SZ_ERROR_DATA => "Data error",
                SevenZipNative.SZ_ERROR_CRC => "CRC error",
                SevenZipNative.SZ_ERROR_PASSWORD => "Wrong password",
                SevenZipNative.SZ_ERROR_THREAD => "Thread error",
                _ => $"Unknown error ({errorCode})"
            };
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SevenZipArchive));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    SevenZipNative.Sz7z_CloseArchive(_handle);
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Archive information
    /// </summary>
    public class ArchiveInfo
    {
        public uint NumItems { get; init; }
        public ulong TotalUnpackSize { get; init; }
        public ulong TotalPackSize { get; init; }
        public bool IsEncrypted { get; init; }
        public bool IsSolid { get; init; }
        public int NumBlocks { get; init; }
    }

    /// <summary>
    /// Archive item information
    /// </summary>
    public class ArchiveItem
    {
        public uint Index { get; init; }
        public string Path { get; init; } = string.Empty;
        public ulong Size { get; init; }
        public ulong PackedSize { get; init; }
        public uint Crc { get; init; }
        public bool IsDirectory { get; init; }
        public bool IsEncrypted { get; init; }
        public DateTime ModifiedTime { get; init; }
        public DateTime? CreatedTime { get; init; }
        public DateTime? AccessedTime { get; init; }
        public uint Attributes { get; init; }

        public double CompressionRatio => Size > 0 ? (double)PackedSize / Size : 0;
    }

    /// <summary>
    /// Exception thrown by 7-Zip operations
    /// </summary>
    public class SevenZipException : Exception
    {
        public int ErrorCode { get; }

        public SevenZipException(string message, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
