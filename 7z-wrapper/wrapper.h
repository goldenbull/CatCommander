#pragma once

// struct for interop
//#pragma pack(push, 1)
struct FormatInfo
{
    wchar_t Name[64];
    wchar_t Ext[256];
    wchar_t AddExt[256];
};
//#pragma pack(pop)

extern "C" {
    wchar_t* GetAllFormatNames(); // names are separated by space
    bool GetFormatInfoByName(wchar_t* name, FormatInfo* info);
}
