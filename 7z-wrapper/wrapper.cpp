/*Sz7zCWrapper.cpp -- Simple C wrapper for 7-Zip COM interface
2025-01-20 : Claude Code
Implementation of the C API wrapper around 7-Zip COM interface.
*/

#include <string>
#include <string_view>
#include <memory>
#include <cctype>
#include <filesystem>
#include <algorithm>

#include "7zVersion.h"

#include "Common/Defs.h"
#include "Common/MyWindows.h"
#include "Common/IntToString.h"
#include "Common/StringConvert.h"
#include "Common/MyString.h"

#include "Windows/PropVariant.h"

#include "7zip/Common/FileStreams.h"
#include "7zip/Archive/IArchive.h"
#include "7zip/IPassword.h"

#include "wrapper.h"

// External API functions from ArchiveExports.cpp
extern "C" {
    HRESULT GetNumberOfFormats(UINT32 *numFormats);
    HRESULT GetHandlerProperty2(UInt32 formatIndex, PROPID propID, PROPVARIANT *value);
}

// Helper function to convert wchar_t to char using C++20 features
// Returns a unique_ptr for automatic memory management
static std::unique_ptr<char[]> WCharToChar(const wchar_t* wstr) {
    if (!wstr) return nullptr;

    size_t len = std::char_traits<wchar_t>::length(wstr);
    auto result = std::make_unique<char[]>(len + 1);

    for (size_t i = 0; i < len; i++) {
        // Simple conversion - only works for ASCII range
        result[i] = static_cast<char>(wstr[i] & 0xFF);
    }
    result[len] = '\0';

    return result;
}

// Helper function for case-insensitive string comparison using C++20
static bool StrEqualNoCase(std::string_view s1, std::string_view s2) {
    if (s1.size() != s2.size()) return false;

    return std::equal(s1.begin(), s1.end(), s2.begin(), s2.end(),
        [](char a, char b) {
            return std::tolower(static_cast<unsigned char>(a)) ==
                   std::tolower(static_cast<unsigned char>(b));
        });
}

// Helper function to check if extension matches any in space-separated list
static bool ExtensionInList(std::string_view ext, std::string_view extList) {
    if (ext.empty() || extList.empty()) return false;

    size_t pos = 0;
    while (pos < extList.size()) {
        // Skip leading spaces
        while (pos < extList.size() && extList[pos] == ' ') ++pos;
        if (pos >= extList.size()) break;

        // Find end of current extension
        size_t end = pos;
        while (end < extList.size() && extList[end] != ' ') ++end;

        // Extract and compare
        auto currentExt = extList.substr(pos, end - pos);
        if (StrEqualNoCase(ext, currentExt)) {
            return true;
        }

        pos = end;
    }

    return false;
}

bool GetArchiveInfo(const char* fname, CArcInfo* outInfo) {
    if (!fname || !outInfo) {
        return false;
    }

    // Extract file extension using std::filesystem::path
    std::filesystem::path filePath(fname);
    std::string extStr = filePath.extension().string();

    // Remove the leading dot if present
    if (!extStr.empty() && extStr[0] == '.') {
        extStr = extStr.substr(1);
    }

    if (extStr.empty()) {
        return false;  // No extension
    }

    std::string_view ext(extStr);

    // Don't handle .exe files
    if (StrEqualNoCase(ext, "exe")) {
        return false;
    }

    // Get number of registered formats
    UINT32 numFormats = 0;
    if (GetNumberOfFormats(&numFormats) != S_OK || numFormats == 0) {
        return false;
    }

    // Search through all registered formats
    for (UINT32 i = 0; i < numFormats; i++) {
        NWindows::NCOM::CPropVariant prop;

        // Get the extension list for this format
        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kExtension, &prop) != S_OK) {
            continue;
        }

        // Check if it's a BSTR (wide string)
        if (prop.vt == VT_BSTR && prop.bstrVal) {
            // Convert BSTR (wchar_t*) to char* for comparison
            auto extListPtr = WCharToChar(prop.bstrVal);
            if (!extListPtr) continue;

            std::string_view extList(extListPtr.get());
            if (!ExtensionInList(ext, extList)) {
                continue;
            }

            // Found a match! Get all properties for this format

            // Get Name
            NWindows::NCOM::CPropVariant nameProp;
            if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kName, &nameProp) == S_OK
                && nameProp.vt == VT_BSTR && nameProp.bstrVal) {
                // Store the converted string - caller must manage memory
                outInfo->Name = WCharToChar(nameProp.bstrVal).release();
            } else {
                outInfo->Name = nullptr;
            }

            // Get Extension - convert from wide string
            outInfo->Ext = WCharToChar(prop.bstrVal).release();

            // Get AddExtension
            NWindows::NCOM::CPropVariant addExtProp;
            if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kAddExtension, &addExtProp) == S_OK
                && addExtProp.vt == VT_BSTR && addExtProp.bstrVal) {
                outInfo->AddExt = WCharToChar(addExtProp.bstrVal).release();
            } else {
                outInfo->AddExt = nullptr;
            }

            // Get Flags
            NWindows::NCOM::CPropVariant flagsProp;
            if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kFlags, &flagsProp) == S_OK
                && flagsProp.vt == VT_UI4) {
                outInfo->Flags = flagsProp.ulVal;
            } else {
                outInfo->Flags = 0;
            }

            // Get TimeFlags
            NWindows::NCOM::CPropVariant timeFlagsProp;
            if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kTimeFlags, &timeFlagsProp) == S_OK
                && timeFlagsProp.vt == VT_UI4) {
                outInfo->TimeFlags = timeFlagsProp.ulVal;
            } else {
                outInfo->TimeFlags = 0;
            }

            // Note: Function pointers and low-level fields are not available via API
            outInfo->CreateInArchive = nullptr;
            outInfo->CreateOutArchive = nullptr;
            outInfo->IsArc = nullptr;
            outInfo->Id = 0;
            outInfo->Signature = nullptr;
            outInfo->SignatureSize = 0;
            outInfo->SignatureOffset = 0;

            return true;
        }
    }

    // No matching format found
    return false;
}