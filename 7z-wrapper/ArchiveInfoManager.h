#pragma once

#include <map>
#include <string>
#include <vector>

#include "Common/MyString.h"
#include "7zip/Common/RegisterArc.h"
#include "wrapper.h"

// this is a copy of internal CArcInfo class, because CArcInfo and g_Arcs are linked internally so not visible from outside
struct ArchiveInfo
{
    std::u16string Name;
    std::u16string Ext;
    std::u16string AddExt;

    UInt32 Flags;
    UInt32 TimeFlags;
    GUID ClassID;
    bool IsMultiSignature() const { return (Flags & NArcInfoFlags::kMultiSignature) != 0; }

    // and alse a simplified member for interop
    FormatInfo simple;
};

// Singleton class to hold Archive Handler Infos
class ArchiveInfoManager
{
public:
    // Get the singleton instance
    static ArchiveInfoManager &getInstance();

    // Get archive info by format name
    bool getArchiveInfoByName(const std::u16string &name, ArchiveInfo &info) const;

    // Get archive info by extension
    bool getArchiveInfoByExtension(const std::u16string &ext, ArchiveInfo &info) const;

    // Check if a format supports an extension
    bool isSupportedFormat(const std::u16string &ext) const;

    // Get all supported format names
    std::vector<std::u16string> getAllFormatNames() const;

    std::u16string all_names;

private:
    ArchiveInfoManager();                                               // Private constructor for singleton
    ~ArchiveInfoManager() = default;                                    // Private destructor for singleton
    ArchiveInfoManager(const ArchiveInfoManager &) = delete;            // Delete copy constructor
    ArchiveInfoManager &operator=(const ArchiveInfoManager &) = delete; // Delete assignment operator

    // Initialize archive information from registered formats
    bool initialize();

    std::map<std::u16string, ArchiveInfo> m_archiveMap;     // Map from format name to ArchiveInfo
    std::map<std::u16string, std::u16string> m_extToFormat; // Map from extension to format name
};