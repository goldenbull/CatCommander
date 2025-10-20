/* Sz7zCWrapper.cpp -- Simple C wrapper for 7-Zip COM interface
2025-01-20 : Claude Code
Implementation of the C API wrapper around 7-Zip COM interface.
*/

#include "StdAfx.h"

#include "Sz7zCWrapper.h"

#include "../../../../7z/C/7zVersion.h"
#include "../../../7z/CPP/Common/Common.h"
#include "../../../7z/CPP/Common/MyInitGuid.h"
#include "../../../7z/CPP/Common/MyString.h"
#include "../../../7z/CPP/Common/StringConvert.h"

#include "../../../7z/CPP/Windows/FileDir.h"
#include "../../../7z/CPP/Windows/FileName.h"
#include "../../../7z/CPP/Windows/PropVariant.h"

#include "../../../7z/CPP/7zip/Archive/IArchive.h"
#include "../../../7z/CPP/7zip/Common/FileStreams.h"
#include "../../../7z/CPP/7zip/ICoder.h"
#include "../../../7z/CPP/7zip/IPassword.h"

// External CreateObject function from DllExports2.cpp
EXTERN_C_BEGIN
STDAPI CreateObject(const GUID *clsid, const GUID *iid, void **outObject);
EXTERN_C_END

using namespace NWindows;

// Define format CLSIDs
Z7_DEFINE_GUID(CLSID_CFormat7z,
    k_7zip_GUID_Data1,
    k_7zip_GUID_Data2,
    k_7zip_GUID_Data3_Common,
    0x01, 0x00, 0x00, 0x01, 0x07, 0x00, 0x00, 0x00);

Z7_DEFINE_GUID(CLSID_CFormatZip,
    k_7zip_GUID_Data1,
    k_7zip_GUID_Data2,
    k_7zip_GUID_Data3_Common,
    0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00);

/* Internal archive handle structure */
struct SzArchiveHandleImpl
{
    CMyComPtr<IInArchive> archive;
    CMyComPtr<IInStream> fileStream;
    UString filePath;
    UString lastError;
    UString password;
};

/* Helper: Convert HRESULT to SZ error code */
static int HResultToSzError(HRESULT hr)
{
    if (hr == S_OK) return SZ_OK;
    if (hr == E_OUTOFMEMORY) return SZ_ERROR_MEM;
    if (hr == E_NOTIMPL) return SZ_ERROR_UNSUPPORTED;
    if (hr == E_INVALIDARG) return SZ_ERROR_PARAM;
    return SZ_ERROR_FAIL;
}

/* Helper: Get string property from archive item */
static HRESULT GetStringProp(IInArchive* archive, UInt32 index, PROPID propID, UString& result)
{
    NWindows::NCOM::CPropVariant prop;
    RINOK(archive->GetProperty(index, propID, &prop))
    if (prop.vt == VT_BSTR && prop.bstrVal)
        result = prop.bstrVal;
    else if (prop.vt == VT_EMPTY)
        result.Empty();
    else
        return E_FAIL;
    return S_OK;
}

/* Helper: Get UInt64 property from archive item */
static HRESULT GetUInt64Prop(IInArchive* archive, UInt32 index, PROPID propID, UInt64& result)
{
    NWindows::NCOM::CPropVariant prop;
    RINOK(archive->GetProperty(index, propID, &prop))
    if (prop.vt == VT_UI8)
        result = prop.uhVal.QuadPart;
    else if (prop.vt == VT_EMPTY)
        result = 0;
    else
        return E_FAIL;
    return S_OK;
}

/* Helper: Get UInt32 property from archive item */
static HRESULT GetUInt32Prop(IInArchive* archive, UInt32 index, PROPID propID, UInt32& result)
{
    NWindows::NCOM::CPropVariant prop;
    RINOK(archive->GetProperty(index, propID, &prop))
    if (prop.vt == VT_UI4)
        result = prop.ulVal;
    else if (prop.vt == VT_EMPTY)
        result = 0;
    else
        return E_FAIL;
    return S_OK;
}

/* Helper: Get bool property from archive item */
static HRESULT GetBoolProp(IInArchive* archive, UInt32 index, PROPID propID, bool& result)
{
    NWindows::NCOM::CPropVariant prop;
    RINOK(archive->GetProperty(index, propID, &prop))
    if (prop.vt == VT_BOOL)
        result = VARIANT_BOOLToBool(prop.boolVal);
    else if (prop.vt == VT_EMPTY)
        result = false;
    else
        return E_FAIL;
    return S_OK;
}

/* Helper: Get FILETIME property from archive item */
static HRESULT GetFileTimeProp(IInArchive* archive, UInt32 index, PROPID propID, UInt64& result)
{
    NWindows::NCOM::CPropVariant prop;
    RINOK(archive->GetProperty(index, propID, &prop))
    if (prop.vt == VT_FILETIME)
    {
        FILETIME ft = prop.filetime;
        result = ((UInt64)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
    }
    else
        result = 0;
    return S_OK;
}

/* Open callback implementation */
class CArchiveOpenCallback Z7_final:
    public IArchiveOpenCallback,
    public ICryptoGetTextPassword,
    public CMyUnknownImp
{
    Z7_COM_QI_BEGIN2(IArchiveOpenCallback)
    Z7_COM_QI_ENTRY(ICryptoGetTextPassword)
    Z7_COM_QI_END
    Z7_COM_ADDREF_RELEASE

    Z7_IFACE_COM7_IMP(IArchiveOpenCallback)
    Z7_IFACE_COM7_IMP(ICryptoGetTextPassword)

public:
    UString Password;

    CArchiveOpenCallback() {}
};

Z7_COM7F_IMF(CArchiveOpenCallback::SetTotal(const UInt64*, const UInt64*))
{
    return S_OK;
}

Z7_COM7F_IMF(CArchiveOpenCallback::SetCompleted(const UInt64*, const UInt64*))
{
    return S_OK;
}

Z7_COM7F_IMF(CArchiveOpenCallback::CryptoGetTextPassword(BSTR* password))
{
    if (!Password.IsEmpty())
    {
        return StringToBstr(Password, password);
    }
    return S_OK;
}

/* Extract callback implementation */
class CArchiveExtractCallback Z7_final:
    public IArchiveExtractCallback,
    public ICryptoGetTextPassword,
    public CMyUnknownImp
{
    Z7_COM_QI_BEGIN2(IArchiveExtractCallback)
    Z7_COM_QI_ENTRY(ICryptoGetTextPassword)
    Z7_COM_QI_END
    Z7_COM_ADDREF_RELEASE

    Z7_IFACE_COM7_IMP(IProgress)
    Z7_IFACE_COM7_IMP(IArchiveExtractCallback)
    Z7_IFACE_COM7_IMP(ICryptoGetTextPassword)

public:
    CMyComPtr<IInArchive> Archive;
    UString OutputDir;
    UString Password;
    SzProgressCallback ProgressCallback;
    void* UserData;

    UInt64 TotalSize;
    UInt64 CompletedSize;

    CMyComPtr<ISequentialOutStream> OutFileStream;

    CArchiveExtractCallback() : ProgressCallback(NULL), UserData(NULL), TotalSize(0), CompletedSize(0) {}
};

Z7_COM7F_IMF(CArchiveExtractCallback::SetTotal(UInt64 size))
{
    TotalSize = size;
    return S_OK;
}

Z7_COM7F_IMF(CArchiveExtractCallback::SetCompleted(const UInt64* completeValue))
{
    if (completeValue && ProgressCallback)
    {
        ProgressCallback(UserData, TotalSize, *completeValue);
    }
    return S_OK;
}

Z7_COM7F_IMF(CArchiveExtractCallback::GetStream(
    UInt32 index,
    ISequentialOutStream** outStream,
    Int32 askExtractMode))
{
    *outStream = NULL;
    OutFileStream.Release();

    if (askExtractMode != NArchive::NExtract::NAskMode::kExtract)
        return S_OK;

    // Get path
    UString path;
    RINOK(GetStringProp(Archive, index, kpidPath, path))

    // Check if directory
    bool isDir = false;
    RINOK(GetBoolProp(Archive, index, kpidIsDir, isDir))

    if (isDir)
        return S_OK;

    // Create full path
    UString fullPath = OutputDir;
    if (!fullPath.IsEmpty() && fullPath.Back() != WCHAR_PATH_SEPARATOR)
        fullPath += WCHAR_PATH_SEPARATOR;
    fullPath += path;

    // Create directories
    int slashPos = fullPath.ReverseFind_PathSepar();
    if (slashPos >= 0)
    {
        UString dirPath = fullPath.Left(slashPos);
        FString dirPathF = us2fs(dirPath);
        NFile::NDir::CreateComplexDir(dirPathF);
    }

    // Create output stream
    COutFileStream* outFileStreamSpec = new COutFileStream;
    CMyComPtr<ISequentialOutStream> outFileStreamLoc(outFileStreamSpec);

    FString fullPathF = us2fs(fullPath);
    if (!outFileStreamSpec->Create_NEW(fullPathF))
        return E_FAIL;

    OutFileStream = outFileStreamLoc;
    *outStream = outFileStreamLoc.Detach();

    return S_OK;
}

Z7_COM7F_IMF(CArchiveExtractCallback::PrepareOperation(Int32))
{
    return S_OK;
}

Z7_COM7F_IMF(CArchiveExtractCallback::SetOperationResult(Int32 opRes))
{
    OutFileStream.Release();

    if (opRes != NArchive::NExtract::NOperationResult::kOK)
    {
        if (opRes == NArchive::NExtract::NOperationResult::kWrongPassword)
            return E_ABORT;
        return E_FAIL;
    }

    return S_OK;
}

Z7_COM7F_IMF(CArchiveExtractCallback::CryptoGetTextPassword(BSTR* password))
{
    if (!Password.IsEmpty())
    {
        return StringToBstr(Password, password);
    }
    return S_OK;
}

/* Exported C functions */

EXTERN_C_BEGIN

int Sz7z_OpenArchive(const wchar_t* filePath, SzArchiveHandle* handle)
{
    if (!filePath || !handle)
        return SZ_ERROR_PARAM;

    *handle = NULL;

    try
    {
        SzArchiveHandleImpl* impl = new SzArchiveHandleImpl;
        if (!impl)
            return SZ_ERROR_MEM;

        impl->filePath = filePath;

        // Open file stream
        CInFileStream* fileStreamSpec = new CInFileStream;
        impl->fileStream = fileStreamSpec;

        // Convert wchar_t path to platform-specific FString
        FString filePathF = us2fs(filePath);
        if (!fileStreamSpec->Open(filePathF))
        {
            delete impl;
            return SZ_ERROR_FAIL;
        }

        // Create archive handler - try to detect format automatically
        // Start with 7z format (most common)
        HRESULT hr = CreateObject(&CLSID_CFormat7z, &IID_IInArchive, (void**)&impl->archive);
        if (FAILED(hr) || !impl->archive)
        {
            delete impl;
            return HResultToSzError(hr);
        }

        // Open archive
        CArchiveOpenCallback* openCallbackSpec = new CArchiveOpenCallback;
        CMyComPtr<IArchiveOpenCallback> openCallback(openCallbackSpec);

        const UInt64 scanSize = 1 << 23;
        hr = impl->archive->Open(impl->fileStream, &scanSize, openCallback);

        if (FAILED(hr))
        {
            delete impl;
            return HResultToSzError(hr);
        }

        *handle = (SzArchiveHandle)impl;
        return SZ_OK;
    }
    catch(...)
    {
        return SZ_ERROR_FAIL;
    }
}

void Sz7z_CloseArchive(SzArchiveHandle handle)
{
    if (!handle)
        return;

    try
    {
        SzArchiveHandleImpl* impl = (SzArchiveHandleImpl*)handle;

        if (impl->archive)
        {
            impl->archive->Close();
            impl->archive.Release();
        }

        if (impl->fileStream)
            impl->fileStream.Release();

        delete impl;
    }
    catch(...)
    {
    }
}

int Sz7z_GetArchiveInfo(SzArchiveHandle handle, SzArchiveInfo* info)
{
    if (!handle || !info)
        return SZ_ERROR_PARAM;

    try
    {
        SzArchiveHandleImpl* impl = (SzArchiveHandleImpl*)handle;

        memset(info, 0, sizeof(SzArchiveInfo));

        // Get number of items
        UInt32 numItems = 0;
        HRESULT hr = impl->archive->GetNumberOfItems(&numItems);
        if (FAILED(hr))
            return HResultToSzError(hr);

        info->numItems = numItems;

        // Calculate total sizes
        for (UInt32 i = 0; i < numItems; i++)
        {
            UInt64 size = 0;
            GetUInt64Prop(impl->archive, i, kpidSize, size);
            info->totalUnpackSize += size;

            UInt64 packSize = 0;
            GetUInt64Prop(impl->archive, i, kpidPackSize, packSize);
            info->totalPackSize += packSize;

            bool isEncrypted = false;
            GetBoolProp(impl->archive, i, kpidEncrypted, isEncrypted);
            if (isEncrypted)
                info->isEncrypted = 1;
        }

        return SZ_OK;
    }
    catch(...)
    {
        return SZ_ERROR_FAIL;
    }
}

int Sz7z_GetItemInfo(SzArchiveHandle handle, UInt32 index, SzItemInfo* info)
{
    if (!handle || !info)
        return SZ_ERROR_PARAM;

    try
    {
        SzArchiveHandleImpl* impl = (SzArchiveHandleImpl*)handle;

        memset(info, 0, sizeof(SzItemInfo));
        info->index = index;

        // Get path
        UString path;
        HRESULT hr = GetStringProp(impl->archive, index, kpidPath, path);
        if (FAILED(hr))
            return HResultToSzError(hr);

        // Note: The path string is owned by the UString and will be invalid
        // after this function returns. Caller should copy it.
        info->path = path.Ptr();

        // Get size
        GetUInt64Prop(impl->archive, index, kpidSize, info->size);
        GetUInt64Prop(impl->archive, index, kpidPackSize, info->packedSize);

        // Get CRC
        GetUInt32Prop(impl->archive, index, kpidCRC, info->crc);

        // Get flags
        bool isDir = false;
        GetBoolProp(impl->archive, index, kpidIsDir, isDir);
        info->isDir = isDir ? 1 : 0;

        bool isEncrypted = false;
        GetBoolProp(impl->archive, index, kpidEncrypted, isEncrypted);
        info->isEncrypted = isEncrypted ? 1 : 0;

        // Get times
        GetFileTimeProp(impl->archive, index, kpidMTime, info->mtime);
        GetFileTimeProp(impl->archive, index, kpidCTime, info->ctime);
        GetFileTimeProp(impl->archive, index, kpidATime, info->atime);

        // Get attributes
        GetUInt32Prop(impl->archive, index, kpidAttrib, info->attributes);

        return SZ_OK;
    }
    catch(...)
    {
        return SZ_ERROR_FAIL;
    }
}

int Sz7z_ExtractItem(
    SzArchiveHandle handle,
    UInt32 index,
    const wchar_t* outputPath,
    SzProgressCallback progressCallback,
    void* userData)
{
    if (!handle || !outputPath)
        return SZ_ERROR_PARAM;

    try
    {
        SzArchiveHandleImpl* impl = (SzArchiveHandleImpl*)handle;

        CArchiveExtractCallback* extractCallbackSpec = new CArchiveExtractCallback;
        CMyComPtr<IArchiveExtractCallback> extractCallback(extractCallbackSpec);

        extractCallbackSpec->Archive = impl->archive;
        extractCallbackSpec->OutputDir = outputPath;
        extractCallbackSpec->Password = impl->password;
        extractCallbackSpec->ProgressCallback = progressCallback;
        extractCallbackSpec->UserData = userData;

        const UInt32 indices[] = { index };
        HRESULT hr = impl->archive->Extract(indices, 1, 0, extractCallback);

        return HResultToSzError(hr);
    }
    catch(...)
    {
        return SZ_ERROR_FAIL;
    }
}

int Sz7z_ExtractAll(
    SzArchiveHandle handle,
    const wchar_t* outputDir,
    SzProgressCallback progressCallback,
    void* userData)
{
    if (!handle || !outputDir)
        return SZ_ERROR_PARAM;

    try
    {
        SzArchiveHandleImpl* impl = (SzArchiveHandleImpl*)handle;

        CArchiveExtractCallback* extractCallbackSpec = new CArchiveExtractCallback;
        CMyComPtr<IArchiveExtractCallback> extractCallback(extractCallbackSpec);

        extractCallbackSpec->Archive = impl->archive;
        extractCallbackSpec->OutputDir = outputDir;
        extractCallbackSpec->Password = impl->password;
        extractCallbackSpec->ProgressCallback = progressCallback;
        extractCallbackSpec->UserData = userData;

        HRESULT hr = impl->archive->Extract(NULL, (UInt32)(Int32)-1, 0, extractCallback);

        return HResultToSzError(hr);
    }
    catch(...)
    {
        return SZ_ERROR_FAIL;
    }
}

int Sz7z_TestArchive(
    SzArchiveHandle handle,
    SzProgressCallback progressCallback,
    void* userData)
{
    if (!handle)
        return SZ_ERROR_PARAM;

    try
    {
        SzArchiveHandleImpl* impl = (SzArchiveHandleImpl*)handle;

        CArchiveExtractCallback* extractCallbackSpec = new CArchiveExtractCallback;
        CMyComPtr<IArchiveExtractCallback> extractCallback(extractCallbackSpec);

        extractCallbackSpec->Archive = impl->archive;
        extractCallbackSpec->Password = impl->password;
        extractCallbackSpec->ProgressCallback = progressCallback;
        extractCallbackSpec->UserData = userData;

        HRESULT hr = impl->archive->Extract(NULL, (UInt32)(Int32)-1, 1, extractCallback);

        return HResultToSzError(hr);
    }
    catch(...)
    {
        return SZ_ERROR_FAIL;
    }
}

int Sz7z_SetPassword(SzArchiveHandle handle, const wchar_t* password)
{
    if (!handle)
        return SZ_ERROR_PARAM;

    try
    {
        SzArchiveHandleImpl* impl = (SzArchiveHandleImpl*)handle;
        impl->password = password ? password : L"";
        return SZ_OK;
    }
    catch(...)
    {
        return SZ_ERROR_FAIL;
    }
}

int Sz7z_GetLastError(SzArchiveHandle handle, wchar_t* buffer, int bufferSize)
{
    if (!buffer || bufferSize <= 0)
        return SZ_ERROR_PARAM;

    try
    {
        if (handle)
        {
            SzArchiveHandleImpl* impl = (SzArchiveHandleImpl*)handle;
            wcsncpy(buffer, impl->lastError.Ptr(), bufferSize - 1);
            buffer[bufferSize - 1] = 0;
        }
        else
        {
            buffer[0] = 0;
        }
        return SZ_OK;
    }
    catch(...)
    {
        return SZ_ERROR_FAIL;
    }
}

int Sz7z_GetVersion(UInt32* major, UInt32* minor)
{
    if (!major || !minor)
        return SZ_ERROR_PARAM;

    *major = MY_VER_MAJOR;
    *minor = MY_VER_MINOR;
    return SZ_OK;
}

int Sz7z_GetSupportedFormats(wchar_t* buffer, int bufferSize)
{
    if (!buffer || bufferSize <= 0)
        return SZ_ERROR_PARAM;

    const wchar_t* formats = L"7z,zip,rar,tar,gz,bz2,xz,iso,cab,arj,lzh";
    wcsncpy(buffer, formats, bufferSize - 1);
    buffer[bufferSize - 1] = 0;

    return SZ_OK;
}

EXTERN_C_END
