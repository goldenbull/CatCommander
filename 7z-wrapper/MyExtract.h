#ifndef EXTRACT_CALLBACK_H
#define EXTRACT_CALLBACK_H

#include "Common/MyCom.h"
#include "Common/MyString.h"
#include "7zip/Archive/IArchive.h"
#include "7zip/IPassword.h"
#include "7zip/IProgress.h"

class CMyExtractCallback : public IArchiveExtractCallback,
                           public ICryptoGetTextPassword,
                           public CMyUnknownImp
{
    Z7_IFACES_IMP_UNK_2(IArchiveExtractCallback, ICryptoGetTextPassword)
    Z7_IFACE_COM7_IMP(IProgress)

private:
    CMyComPtr<IInArchive> _archiveHandler;
    FString _directoryPath;
    CMyComPtr<ISequentialOutStream> _outFileStream;

public:
    CMyExtractCallback() {};
    virtual ~CMyExtractCallback() {};
    void Init(IInArchive *archive, const FString &directoryPath);

};

#endif // EXTRACT_CALLBACK_H
