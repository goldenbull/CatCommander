#pragma once

// struct for interop
//#pragma pack(push, 1)
struct FormatInfo
{
    char16_t Name[64];
    char16_t Ext[256];
    char16_t AddExt[256];
};
//#pragma pack(pop)

extern "C" {
    char16_t* GetAllFormatNames(); // names are separated by space
    bool GetFormatInfoByName(char16_t* name, FormatInfo* info);
}
