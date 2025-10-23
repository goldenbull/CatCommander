#include <string>
#include <string_view>
#include <memory>
#include <cctype>
#include <filesystem>
#include <algorithm>
#include <iostream>

#include "uni_algo/conv.h"

#include "ArchiveInfoManager.h"
#include "wrapper.h"
#include "MyExtract.h"

#include "Common/MyCom.h"
#include "7zip/Archive/IArchive.h"
#include "7zip/Common/FileStreams.h"
#include "Windows/FileDir.h"
#include "7zip/IPassword.h"
#include "7zip/IProgress.h"
#include "7zip/Common/ProgressUtils.h"
#include "Windows/PropVariant.h"

bool GetFormatInfoByName(char16_t *name, FormatInfo *info)
{
    if (!name || !info)
    {
        return false;
    }

    // Use the singleton to get archive info by extension
    ArchiveInfoManager &manager = ArchiveInfoManager::getInstance();
    ArchiveInfo arc_info = {};
    if (!manager.getArchiveInfoByName(name, arc_info))
    {
        return false; // No matching format found
    }

    *info = arc_info.simple;
    return true;
}

char16_t *GetAllFormatNames()
{
    ArchiveInfoManager &manager = ArchiveInfoManager::getInstance();
    return manager.all_names.data();
}

STDAPI CreateObject(const GUID *clsid, const GUID *iid, void **outObject);

bool TestExpandToCurrentFolder(const char16_t *filename)
{
    if (!filename)
        return false;

    std::filesystem::path filePath(filename);

    // Check if the file exists
    if (!std::filesystem::exists(filePath))
    {
        std::wcout << L"File does not exist: " << filePath << std::endl;
        return false;
    }

    ArchiveInfoManager &manager = ArchiveInfoManager::getInstance();

    // Get file extension to determine archive type
    auto ext = filePath.extension().u16string();
    if (!ext.empty() && ext[0] == '.')
        ext = ext.substr(1);

    // Find the appropriate archive handler based on the extension
    ArchiveInfo info;
    if (!manager.getArchiveInfoByExtension(ext, info))
    {
        std::wcout << L"Cannot find appropriate archive handler for extension: " << una::utf16to32(ext) << std::endl;
        return false;
    }

    // Create the IInArchive object for the appropriate format
    CMyComPtr<IInArchive> archive;
    if (CreateObject(&info.ClassID, &IID_IInArchive, (void **)&archive) != S_OK)
    {
        std::wcout << L"Cannot get class object for format: " << una::utf16to32(info.Name) << std::endl;
        return false;
    }

    // Open the archive file
    CInFileStream *fs = new CInFileStream();
    CMyComPtr<IInStream> fileStream(fs);

    // Convert std::filesystem::path to the required format for 7-Zip
    std::string utf8Path = una::utf16to8(filePath.u16string());
    if (!fs->Open(utf8Path.c_str()))
    {
        std::wcout << L"Cannot open archive file: " << filePath << std::endl;
        return false;
    }

    // Open the archive with a null callback (no password or additional callbacks)
    const UInt64 scanSize = 1 << 24; // 16MB scan size
    if (archive->Open(fileStream, &scanSize, NULL) != S_OK)
    {
        std::wcout << L"Cannot open file as archive: " << filePath << std::endl;
        return false;
    }

    // Get number of items in the archive
    UInt32 numItems = 0;
    if (archive->GetNumberOfItems(&numItems) != S_OK || numItems == 0)
    {
        std::wcout << L"Archive is empty or error getting number of items" << std::endl;
        return false;
    }

    // Get directory of the archive file as the extraction path
    std::filesystem::path archiveDir = filePath.parent_path();
    FString extractPath = us2fs(archiveDir.wstring().data());

    // Create and initialize the extraction callback
    CMyExtractCallback *callbackSpec = new CMyExtractCallback();
    CMyComPtr<IArchiveExtractCallback> extractCallback(callbackSpec);
    callbackSpec->Init(archive, extractPath);

    // Extract all items (NULL means all, -1 means all items, false means extract not test)
    HRESULT result = archive->Extract(NULL, (UInt32)(Int32)(-1), false, extractCallback);

    if (result != S_OK)
    {
        std::wcout << L"Extraction failed with HRESULT: " << result << std::endl;
        return false;
    }

    return true;
}
