#include <string>
#include <string_view>
#include <memory>
#include <cctype>
#include <filesystem>
#include <algorithm>
#include <vector>
#include <iostream>
#include <ranges>

#include "Windows/PropVariant.h"
#include "7zip/Archive/IArchive.h"

#include "utils.h"
#include "ArchiveInfoManager.h"

// External API functions from ArchiveExports.cpp
STDAPI GetNumberOfFormats(UINT32* numFormats);
STDAPI GetHandlerProperty2(UInt32 formatIndex, PROPID propID, PROPVARIANT* value);

// Implementation of the ArchiveInfoManager singleton
ArchiveInfoManager::ArchiveInfoManager()
{
    // Initialize in constructor to ensure data is ready
    initialize();
}

ArchiveInfoManager& ArchiveInfoManager::getInstance()
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
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kName, &prop) == S_OK && prop.vt == VT_BSTR || prop.bstrVal)
        {
            info.Name = prop.bstrVal; // BSTR is wchar_t*
            all_names = all_names + info.Name + L" ";
        }

        // Get Extension
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kExtension, &prop) == S_OK && prop.vt == VT_BSTR && prop.bstrVal)
        {
            info.Ext = prop.bstrVal;
        }

        // Get AddExtension
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kAddExtension, &prop) == S_OK && prop.vt == VT_BSTR && prop.bstrVal)
        {
            info.AddExt = prop.bstrVal;
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

        // fill interop struct
        std::ranges::copy(info.Name, info.simple.Name);
        std::ranges::copy(info.Ext, info.simple.Ext);
        std::ranges::copy(info.AddExt, info.simple.AddExt);

        // Add to the map
        m_archiveMap[info.Name] = info;

        // Create mapping from extensions to format name
        if (!info.Ext.empty())
        {
            for (auto ext : std::views::split(info.Ext, L" "))
            {
                m_extToFormat[strlower(std::wstring_view(ext))] = info.Name;
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

bool ArchiveInfoManager::getArchiveInfoByName(const std::wstring& name, ArchiveInfo& info) const
{
    auto it = m_archiveMap.find(name);
    if (it != m_archiveMap.end())
    {
        info = it->second;
        return true;
    }
    return false;
}

bool ArchiveInfoManager::getArchiveInfoByExtension(const std::wstring& ext, ArchiveInfo& info) const
{
    auto it = m_extToFormat.find(strlower(ext));
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

bool ArchiveInfoManager::isSupportedFormat(const std::wstring& ext) const
{
    return m_extToFormat.contains(strlower(ext));
}


std::vector<std::wstring> ArchiveInfoManager::getAllFormatNames() const
{
    std::vector<std::wstring> formatNames;
    formatNames.reserve(m_archiveMap.size());

    for (const auto& key : m_archiveMap | std::views::keys)
    {
        formatNames.push_back(key);
    }

    return formatNames;
}
