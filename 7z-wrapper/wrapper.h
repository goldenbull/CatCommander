#pragma once

#include "Common/MyString.h"
#include "7zip/Common/RegisterArc.h"

// Wrapper structure for archive info using FString instead of const char*
struct ArchiveInfo
{
    FString Name;
    FString Ext;
    FString AddExt;

    UInt32 Flags;
    UInt32 TimeFlags;

    // Note: Function pointers and low-level fields not available via exported API
    Byte Id;
    Byte SignatureSize;
    UInt16 SignatureOffset;
    const Byte *Signature;

    Func_CreateInArchive CreateInArchive;
    Func_CreateOutArchive CreateOutArchive;
    Func_IsArc IsArc;
};

extern "C"
{
    bool GetArchiveInfo(const char* fname, ArchiveInfo* info);
}
