#include "7zip/Archive/IArchive.h"
#include "7zip/Common/FileStreams.h"
#include "7zip/IPassword.h"
#include "7zip/IProgress.h"
#include "7zip/Common/ProgressUtils.h"
#include "Windows/PropVariant.h"
#include "Common/MyCom.h"
#include "Windows/FileDir.h"

#include "MyExtract.h"

STDMETHODIMP CMyExtractCallback::GetStream(UInt32 index, ISequentialOutStream **outStream, Int32 askExtractMode)
{
    *outStream = NULL;

    if (askExtractMode != NArchive::NExtract::NAskMode::kExtract)
        return S_OK;

    // Get file path from archive
    NWindows::NCOM::CPropVariant propVariant;
    RINOK(_archiveHandler->GetProperty(index, kpidPath, &propVariant))

    UString fullPath;
    if (propVariant.vt == VT_BSTR)
        fullPath = propVariant.bstrVal;
    else if (propVariant.vt != VT_EMPTY)
        return E_FAIL;

    // Get if file is folder
    bool isDir = false;
    propVariant.Clear();
    if (_archiveHandler->GetProperty(index, kpidIsDir, &propVariant) == S_OK)
    {
        if (propVariant.vt == VT_BOOL)
        {
            isDir = (propVariant.boolVal != VARIANT_FALSE);
        }
        else if (propVariant.vt != VT_EMPTY)
        {
            return E_FAIL;
        }
    }

    // Construct the output path
    FString fullProcessedPath = _directoryPath + FTEXT("\\") + us2fs(fullPath);

    if (isDir)
    {
        NWindows::NFile::NDir::CreateComplexDir(fullProcessedPath);
        return S_OK;
    }

    // Ensure parent directory exists
    int pos = fullProcessedPath.ReverseFind(FTEXT('\\'));
    if (pos >= 0)
    {
        FString parentDir = fullProcessedPath.Left(pos);
        if (!NWindows::NFile::NDir::CreateComplexDir(parentDir))
            return E_FAIL;
    }

    // Create file stream for output
    COutFileStream *outFileStreamSpec = new COutFileStream;
    CMyComPtr<ISequentialOutStream> outFileStream(outFileStreamSpec);

    if (!outFileStreamSpec->Create_ALWAYS(fullProcessedPath))
    {
        return S_FALSE; // Don't stop extraction for single file error
    }

    _outFileStream = outFileStream;
    *outStream = outFileStream.Detach();
    return S_OK;
}

STDMETHODIMP CMyExtractCallback::PrepareOperation(Int32 askExtractMode)
{
    return S_OK;
}

STDMETHODIMP CMyExtractCallback::SetOperationResult(Int32 resultEOperationResult)
{
    _outFileStream.Release();
    return S_OK;
}

STDMETHODIMP CMyExtractCallback::CryptoGetTextPassword(BSTR *password)
{
    *password = NULL; // No password support for this test
    return E_NOTIMPL;
}

void CMyExtractCallback::Init(IInArchive *archive, const FString &directoryPath)
{
    _archiveHandler = archive;
    _directoryPath = directoryPath;
}

STDMETHODIMP CMyExtractCallback::SetTotal(UInt64 size)
{
    return S_OK;
}

STDMETHODIMP CMyExtractCallback::SetCompleted(const UInt64 *completeValue)
{
    return S_OK;
}
