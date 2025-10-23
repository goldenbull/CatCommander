#include <string>
#include <string_view>
#include <memory>
#include <cctype>
#include <filesystem>
#include <algorithm>
#include <vector>
#include <iostream>
#include <ranges>

#include "uni_algo/conv.h"
#include "uni_algo/case.h"

#include "Windows/PropVariant.h"
#include "7zip/Archive/IArchive.h"

#include "ArchiveInfoManager.h"

// External API functions from ArchiveExports.cpp
STDAPI GetNumberOfFormats(UINT32 *numFormats);
STDAPI GetHandlerProperty2(UInt32 formatIndex, PROPID propID, PROPVARIANT *value);

// Implementation of the ArchiveInfoManager singleton
ArchiveInfoManager::ArchiveInfoManager()
{
    // Initialize in constructor to ensure data is ready
    initialize();
}

ArchiveInfoManager &ArchiveInfoManager::getInstance()
{
    static ArchiveInfoManager instance; // Guaranteed to be destroyed and instantiated on first use
    return instance;
}

bool ArchiveInfoManager::initialize()
{
    // Get number of registered formats
    UINT32 numFormats = 0;
    GetNumberOfFormats(&numFormats);

    // Process all registered formats
    for (UINT32 i = 0; i < numFormats; i++)
    {
        ArchiveInfo info = {};
        NWindows::NCOM::CPropVariant prop;

        // Get format name
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kName, &prop) == S_OK && prop.vt == VT_BSTR && prop.bstrVal)
        {
            std::wstring s(prop.bstrVal); // BSTR is wchar_t*
            info.Name = std::u16string(s.begin(), s.end());
            all_names = all_names + info.Name + u" ";
        }

        // Get Extension
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kExtension, &prop) == S_OK && prop.vt == VT_BSTR && prop.bstrVal)
        {
            std::wstring s(prop.bstrVal);
            info.Ext = std::u16string(s.begin(), s.end());
        }

        // Get AddExtension
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kAddExtension, &prop) == S_OK && prop.vt == VT_BSTR && prop.bstrVal)
        {
            std::wstring s(prop.bstrVal);
            info.AddExt = std::u16string(s.begin(), s.end());
        }

        // Get Flags
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kFlags, &prop) == S_OK && prop.vt == VT_UI4)
        {
            info.Flags = prop.ulVal;
        }

        // Get TimeFlags
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kTimeFlags, &prop) == S_OK && prop.vt == VT_UI4)
        {
            info.TimeFlags = prop.ulVal;
        }

        // Get Class ID
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kClassID, &prop) == S_OK && prop.vt == VT_BSTR && prop.bstrVal)
        {
            memcpy(&info.ClassID, prop.bstrVal, sizeof(GUID));
        }

        // fill interop struct
        std::ranges::copy(info.Name, info.simple.Name);
        std::ranges::copy(info.Ext, info.simple.Ext);
        std::ranges::copy(info.AddExt, info.simple.AddExt);
        info.simple.ClassID = info.ClassID;

        // Add to the map
        m_archiveMap[info.Name] = info;

        // Create mapping from extensions to format name
        if (!info.Ext.empty())
        {
            for (auto ext : std::views::split(info.Ext, L" "))
            {
                m_extToFormat[una::cases::to_lowercase_utf16(std::u16string_view(ext))] = info.Name;
            }
        }
    }

    // remove last space
    if (!all_names.empty())
    {
        all_names.pop_back();
    }

    return true;
}

bool ArchiveInfoManager::getArchiveInfoByName(const std::u16string &name, ArchiveInfo &info) const
{
    auto it = m_archiveMap.find(name);
    if (it != m_archiveMap.end())
    {
        info = it->second;
        return true;
    }
    return false;
}

bool ArchiveInfoManager::getArchiveInfoByExtension(const std::u16string &ext, ArchiveInfo &info) const
{
    auto it = m_extToFormat.find(una::cases::to_lowercase_utf16(ext));
    if (it != m_extToFormat.end())
    {
        auto infoIt = m_archiveMap.find(it->second);
        if (infoIt != m_archiveMap.end())
        {
            info = infoIt->second;
            return true;
        }
    }

    return false;
}

bool ArchiveInfoManager::isSupportedFormat(const std::u16string &ext) const
{
    return m_extToFormat.contains(una::cases::to_lowercase_utf16(ext));
}

std::vector<std::u16string> ArchiveInfoManager::getAllFormatNames() const
{
    std::vector<std::u16string> formatNames;
    formatNames.reserve(m_archiveMap.size());

    for (const auto &key : m_archiveMap | std::views::keys)
    {
        formatNames.push_back(key);
    }

    return formatNames;
}
