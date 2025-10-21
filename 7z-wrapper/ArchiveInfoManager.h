#pragma once

#include <map>
#include <string>
#include <vector>

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

// Singleton class to hold Archive Handler Infos
class ArchiveInfoManager
{
public:
    // Get the singleton instance
    static ArchiveInfoManager& getInstance();
    
    // Get archive info by format name
    bool getArchiveInfoByName(const std::string& name, ArchiveInfo& info) const;
    
    // Get archive info by extension
    bool getArchiveInfoByExtension(const std::string& ext, ArchiveInfo& info) const;
    
    // Check if a format supports an extension
    bool isSupportedFormat(const std::string& ext) const;
    
    // Get all supported format names
    std::vector<std::string> getAllFormatNames() const;

private:
    ArchiveInfoManager();  // Private constructor for singleton
    ~ArchiveInfoManager(); // Private destructor for singleton
    ArchiveInfoManager(const ArchiveInfoManager&) = delete;             // Delete copy constructor
    ArchiveInfoManager& operator=(const ArchiveInfoManager&) = delete;  // Delete assignment operator
    
    // Initialize archive information from registered formats
    bool initialize();
    
    std::map<std::string, ArchiveInfo> m_archiveMap;  // Map from format name to ArchiveInfo
    std::map<std::string, std::string> m_extToFormat; // Map from extension to format name
    bool m_initialized;
};