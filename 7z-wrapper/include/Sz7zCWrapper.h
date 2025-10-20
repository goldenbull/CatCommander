/* Sz7zCWrapper.h -- Simple C wrapper for 7-Zip COM interface
2025-01-20 : Claude Code
This file provides a simple C API wrapper around the 7-Zip COM interface
for easier P/Invoke usage from C# and other languages.
*/

#ifndef SZ_7Z_C_WRAPPER_H
#define SZ_7Z_C_WRAPPER_H

#include "7zTypes.h"

EXTERN_C_BEGIN

/* Use 7z's standard error codes from 7zTypes.h */
/* Additional error code for password-related errors */
#define SZ_ERROR_PASSWORD 100

/* Archive handle - opaque pointer */
typedef void* SzArchiveHandle;

/* Extract callback function types */
typedef void (*SzProgressCallback)(void* userData, UInt64 total, UInt64 completed);
typedef int (*SzExtractCallback)(void* userData, UInt32 index, const wchar_t* path, UInt64 size);
typedef int (*SzPasswordCallback)(void* userData, wchar_t* passwordBuf, int passwordBufSize);

/* Archive information structure */
typedef struct
{
    UInt32 numItems;
    UInt64 totalUnpackSize;
    UInt64 totalPackSize;
    int isEncrypted;
    int isSolid;
    int numBlocks;
    const wchar_t* formatName;
} SzArchiveInfo;

/* Item information structure */
typedef struct
{
    UInt32 index;
    const wchar_t* path;
    UInt64 size;
    UInt64 packedSize;
    UInt32 crc;
    int isDir;
    int isEncrypted;
    UInt64 mtime; // Windows FILETIME format
    UInt64 ctime;
    UInt64 atime;
    UInt32 attributes;
} SzItemInfo;

/*
  Sz7z_OpenArchive
  Opens an archive file for reading.

  Parameters:
    filePath: Path to the archive file (UTF-16 on Windows, UTF-8 on Unix)
    handle: Receives the archive handle on success

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_OpenArchive(const wchar_t* filePath, SzArchiveHandle* handle);

/*
  Sz7z_CloseArchive
  Closes an open archive and releases all resources.

  Parameters:
    handle: Archive handle to close
*/
void Sz7z_CloseArchive(SzArchiveHandle handle);

/*
  Sz7z_GetArchiveInfo
  Gets information about the archive.

  Parameters:
    handle: Archive handle
    info: Receives archive information

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_GetArchiveInfo(SzArchiveHandle handle, SzArchiveInfo* info);

/*
  Sz7z_GetItemInfo
  Gets information about a specific item in the archive.

  Parameters:
    handle: Archive handle
    index: Item index (0-based)
    info: Receives item information

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_GetItemInfo(SzArchiveHandle handle, UInt32 index, SzItemInfo* info);

/*
  Sz7z_ExtractItem
  Extracts a single item to a file.

  Parameters:
    handle: Archive handle
    index: Item index to extract
    outputPath: Destination file path
    progressCallback: Optional progress callback (can be NULL)
    userData: User data passed to callback

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_ExtractItem(
    SzArchiveHandle handle,
    UInt32 index,
    const wchar_t* outputPath,
    SzProgressCallback progressCallback,
    void* userData);

/*
  Sz7z_ExtractAll
  Extracts all items to a directory.

  Parameters:
    handle: Archive handle
    outputDir: Destination directory path
    progressCallback: Optional progress callback (can be NULL)
    userData: User data passed to callback

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_ExtractAll(
    SzArchiveHandle handle,
    const wchar_t* outputDir,
    SzProgressCallback progressCallback,
    void* userData);

/*
  Sz7z_TestArchive
  Tests the integrity of the archive without extracting.

  Parameters:
    handle: Archive handle
    progressCallback: Optional progress callback (can be NULL)
    userData: User data passed to callback

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_TestArchive(
    SzArchiveHandle handle,
    SzProgressCallback progressCallback,
    void* userData);

/*
  Sz7z_SetPassword
  Sets the password for encrypted archives.

  Parameters:
    handle: Archive handle
    password: Password string (UTF-16)

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_SetPassword(SzArchiveHandle handle, const wchar_t* password);

/*
  Sz7z_GetLastError
  Gets the last error message.

  Parameters:
    handle: Archive handle (can be NULL for global errors)
    buffer: Buffer to receive error message
    bufferSize: Size of buffer in characters

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_GetLastError(SzArchiveHandle handle, wchar_t* buffer, int bufferSize);

/*
  Sz7z_GetVersion
  Gets the 7-Zip version information.

  Parameters:
    major: Receives major version number
    minor: Receives minor version number

  Returns:
    SZ_OK on success
*/
int Sz7z_GetVersion(UInt32* major, UInt32* minor);

/*
  Sz7z_GetSupportedFormats
  Gets a list of supported archive formats.

  Parameters:
    formats: Buffer to receive format names (comma-separated)
    bufferSize: Size of buffer in characters

  Returns:
    SZ_OK on success, error code otherwise
*/
int Sz7z_GetSupportedFormats(wchar_t* buffer, int bufferSize);

EXTERN_C_END

#endif /* SZ_7Z_C_WRAPPER_H */
