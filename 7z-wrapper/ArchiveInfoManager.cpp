#include <string>
#include <string_view>
#include <memory>
#include <cctype>
#include <filesystem>
#include <algorithm>
#include <vector>

#include "Windows/PropVariant.h"
#include "7zip/Common/FileStreams.h"
#include "7zip/Archive/IArchive.h"

#include "ArchiveInfoManager.h"

#include <iostream>

// External API functions from ArchiveExports.cpp
STDAPI GetNumberOfFormats(UINT32 *numFormats);
STDAPI GetHandlerProperty2(UInt32 formatIndex, PROPID propID, PROPVARIANT *value);

// Implementation of the ArchiveInfoManager singleton
ArchiveInfoManager::ArchiveInfoManager() : m_initialized(false) {
    // Initialize in constructor to ensure data is ready
    initialize();
}

ArchiveInfoManager::~ArchiveInfoManager() {
    // Clean up any resources if needed
}

ArchiveInfoManager &ArchiveInfoManager::getInstance() {
    static ArchiveInfoManager instance; // Guaranteed to be destroyed and instantiated on first use
    return instance;
}

bool ArchiveInfoManager::initialize() {
    if (m_initialized) {
        return true; // Already initialized
    }

    // Get number of registered formats
    UINT32 numFormats = 0;
    if (GetNumberOfFormats(&numFormats) != S_OK || numFormats == 0) {
        return false;
    }

    // Process all registered formats
    for (UINT32 i = 0; i < numFormats; i++) {
        NWindows::NCOM::CPropVariant nameProp;

        // Get format name
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kName, &nameProp) != S_OK
            || nameProp.vt != VT_BSTR || !nameProp.bstrVal) {
            continue;
        }

        // Create ArchiveInfo structure
        ArchiveInfo info;
        info.Name = nameProp.bstrVal; // Already have the name as UString

        // Get Extension
        NWindows::NCOM::CPropVariant extProp;
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kExtension, &extProp) == S_OK
            && extProp.vt == VT_BSTR && extProp.bstrVal) {
            info.Ext = extProp.bstrVal; // Direct assignment to UString
        } else {
            info.Ext.Empty();
        }

        // Get AddExtension
        NWindows::NCOM::CPropVariant addExtProp;
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kAddExtension, &addExtProp) == S_OK
            && addExtProp.vt == VT_BSTR && addExtProp.bstrVal) {
            info.AddExt = addExtProp.bstrVal; // Direct assignment to UString
        } else {
            info.AddExt.Empty();
        }

        // Get Flags
        NWindows::NCOM::CPropVariant flagsProp;
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kFlags, &flagsProp) == S_OK
            && flagsProp.vt == VT_UI4) {
            info.Flags = flagsProp.ulVal;
        } else {
            info.Flags = 0;
        }

        // Get TimeFlags
        NWindows::NCOM::CPropVariant timeFlagsProp;
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kTimeFlags, &timeFlagsProp) == S_OK
            && timeFlagsProp.vt == VT_UI4) {
            info.TimeFlags = timeFlagsProp.ulVal;
        } else {
            info.TimeFlags = 0;
        }

        // Note: Function pointers and low-level fields are not available via API
        info.CreateInArchive = nullptr;
        info.CreateOutArchive = nullptr;
        info.IsArc = nullptr;
        info.Id = 0;
        info.Signature = nullptr;
        info.SignatureSize = 0;
        info.SignatureOffset = 0;

        // Add to the map
        m_archiveMap[info.Name.Ptr()] = info;

        // Create mapping from extensions to format name
        if (!info.Ext.IsEmpty()) {
            // Process the extension string and map each extension
            const wchar_t *extListView = info.Ext;

            size_t pos = 0;
            size_t len = info.Ext.Len();
            while (pos < len) {
                // Skip leading spaces
                while (pos < len && extListView[pos] == L' ') ++pos;
                if (pos >= len) break;

                // Find end of current extension
                size_t end = pos;
                while (end < len && extListView[end] != L' ') ++end;

                // Extract and map extension
                std::wstring currentExt(extListView + pos, end - pos);
                if (!currentExt.empty()) {
                    std::wstring extStr = currentExt;
                    std::ranges::transform(extStr, extStr.begin(), [](wchar_t c) { return std::tolower(c); });
                    m_extToFormat[extStr] = info.Name.Ptr();
                }

                pos = end + 1; // Move past the space
            }
        }
    }

    m_initialized = true;
    return true;
}

bool ArchiveInfoManager::getArchiveInfoByName(const std::wstring &name, ArchiveInfo &info) const {
    if (!m_initialized) {
        const_cast<ArchiveInfoManager *>(this)->initialize(); // Lazy initialization
    }

    auto it = m_archiveMap.find(name);
    if (it != m_archiveMap.end()) {
        info = it->second;
        return true;
    }
    return false;
}

bool ArchiveInfoManager::getArchiveInfoByExtension(const std::wstring &ext, ArchiveInfo &info) const {
    if (!m_initialized) {
        const_cast<ArchiveInfoManager *>(this)->initialize(); // Lazy initialization
    }

    std::wstring lowerExt = ext;
    std::ranges::transform(lowerExt, lowerExt.begin(), [](wchar_t c) { return std::tolower(c); });

    auto it = m_extToFormat.find(lowerExt);
    if (it != m_extToFormat.end()) {
        auto infoIt = m_archiveMap.find(it->second);
        if (infoIt != m_archiveMap.end()) {
            info = infoIt->second;
            return true;
        }
    }

    return false;
}

bool ArchiveInfoManager::isSupportedFormat(const std::wstring &ext) const {
    if (!m_initialized) {
        const_cast<ArchiveInfoManager *>(this)->initialize(); // Lazy initialization
    }

    std::wstring lowerExt = ext;
    std::ranges::transform(lowerExt, lowerExt.begin(), [](wchar_t c) { return std::tolower(c); });

    return m_extToFormat.find(lowerExt) != m_extToFormat.end();
}


std::vector<std::wstring> ArchiveInfoManager::getAllFormatNames() const {
    std::vector<std::wstring> formatNames;
    formatNames.reserve(m_archiveMap.size());

    for (const auto &pair: m_archiveMap) {
        formatNames.push_back(pair.first);
    }

    return formatNames;
}
