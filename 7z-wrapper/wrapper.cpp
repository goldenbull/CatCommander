#include <string>
#include <string_view>
#include <memory>
#include <cctype>
#include <filesystem>
#include <algorithm>

#include "ArchiveInfoManager.h"
#include "wrapper.h"


bool GetFormatInfoByName(wchar_t* name, FormatInfo* info)
{
   if (!name || !info) {
        return false;
    }

    // Use the singleton to get archive info by extension
    ArchiveInfoManager &manager = ArchiveInfoManager::getInstance();
    ArchiveInfo arc_info;
    if (!manager.getArchiveInfoByName(name, arc_info)) {
        return false; // No matching format found
    }

    *info = arc_info.simple;
    return true;
}

wchar_t* GetAllFormatNames()
{
    ArchiveInfoManager& manager = ArchiveInfoManager::getInstance();
    return manager.all_names.data();
}