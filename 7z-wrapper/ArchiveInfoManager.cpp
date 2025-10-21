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

// External API functions from ArchiveExports.cpp
extern "C" {
HRESULT GetNumberOfFormats(UINT32 *numFormats);
HRESULT GetHandlerProperty2(UInt32 formatIndex, PROPID propID, PROPVARIANT *value);
}

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

        // Convert name to string for use as map key
        std::string formatName{us2fs(nameProp.bstrVal)};

        // Create ArchiveInfo structure
        ArchiveInfo info;
        info.Name = us2fs(nameProp.bstrVal); // Already have the name

        // Get Extension
        NWindows::NCOM::CPropVariant extProp;
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kExtension, &extProp) == S_OK
            && extProp.vt == VT_BSTR && extProp.bstrVal) {
            info.Ext = us2fs(extProp.bstrVal);
        } else {
            info.Ext.Empty();
        }

        // Get AddExtension
        NWindows::NCOM::CPropVariant addExtProp;
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kAddExtension, &addExtProp) == S_OK
            && addExtProp.vt == VT_BSTR && addExtProp.bstrVal) {
            info.AddExt = us2fs(addExtProp.bstrVal);
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
        m_archiveMap[formatName] = info;

        // Create mapping from extensions to format name
        if (!info.Ext.IsEmpty()) {
            // Split the extension string and map each extension
            FString extList(info.Ext);
            std::string_view extListView(static_cast<const char *>(extList), extList.Len());

            size_t pos = 0;
            while (pos < extListView.size()) {
                // Skip leading spaces
                while (pos < extListView.size() && extListView[pos] == ' ') ++pos;
                if (pos >= extListView.size()) break;

                // Find end of current extension
                size_t end = pos;
                while (end < extListView.size() && extListView[end] != ' ') ++end;

                // Extract and map extension
                auto currentExt = extListView.substr(pos, end - pos);
                if (!currentExt.empty()) {
                    // Convert to lowercase for case-insensitive matching
                    std::string extStr = std::string(currentExt);
                    std::transform(extStr.begin(), extStr.end(), extStr.begin(),
                                   [](unsigned char c) { return std::tolower(c); });
                    m_extToFormat[extStr] = formatName;
                }

                pos = end;
            }
        }
    }

    m_initialized = true;
    return true;
}

bool ArchiveInfoManager::getArchiveInfoByName(const std::string &name, ArchiveInfo &info) const {
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

bool ArchiveInfoManager::getArchiveInfoByExtension(const std::string &ext, ArchiveInfo &info) const {
    if (!m_initialized) {
        const_cast<ArchiveInfoManager *>(this)->initialize(); // Lazy initialization
    }

    std::string lowerExt = ext;
    std::transform(lowerExt.begin(), lowerExt.end(), lowerExt.begin(),
                   [](unsigned char c) { return std::tolower(c); });

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

bool ArchiveInfoManager::isSupportedFormat(const std::string &ext) const {
    if (!m_initialized) {
        const_cast<ArchiveInfoManager *>(this)->initialize(); // Lazy initialization
    }

    std::string lowerExt = ext;
    std::transform(lowerExt.begin(), lowerExt.end(), lowerExt.begin(),
                   [](unsigned char c) { return std::tolower(c); });

    return m_extToFormat.find(lowerExt) != m_extToFormat.end();
}


std::vector<std::string> ArchiveInfoManager::getAllFormatNames() const {
    std::vector<std::string> formatNames;
    formatNames.reserve(m_archiveMap.size());
    
    for (const auto& pair : m_archiveMap) {
        formatNames.push_back(pair.first);
    }
    
    return formatNames;
}
