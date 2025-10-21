/* main.cpp -- Test program for 7z-wrapper
2025-01-21 : Claude Code
Simple test program to verify GetArchiveInfo functionality.
*/

#include <iostream>
#include <format>
#include <string_view>
#include <vector>
#include <ranges>
#include <span>

#include "Common/MyWindows.h"
#include "Windows/PropVariant.h"
#include "7zip/Archive/IArchive.h"

#include "wrapper.h"

// External API functions for testing
extern "C" {
    HRESULT GetNumberOfFormats(UINT32 *numFormats);
    HRESULT GetHandlerProperty2(UInt32 formatIndex, PROPID propID, PROPVARIANT *value);
}

void testFile(std::string_view filename) {
    std::cout << std::format("\nTesting: {}\n", filename);
    std::cout << "=====================================\n";

    ArchiveInfo info;

    if (GetArchiveInfo(filename.data(), &info)) {
        std::cout << "✓ Format detected!\n";
        std::cout << std::format("  Name:      {}\n",
            info.Name.IsEmpty() ? "(null)" : static_cast<const char*>(info.Name));
        std::cout << std::format("  Ext:       {}\n",
            info.Ext.IsEmpty() ? "(null)" : static_cast<const char*>(info.Ext));
        std::cout << std::format("  AddExt:    {}\n",
            info.AddExt.IsEmpty() ? "(null)" : static_cast<const char*>(info.AddExt));
        std::cout << std::format("  Flags:     0x{:08X}\n", info.Flags);
        std::cout << std::format("  TimeFlags: 0x{:08X}\n", info.TimeFlags);
    } else {
        std::cout << "✗ No format detected (unknown or unsupported)\n";
    }
}

void listAllFormats() {
    std::cout << "\nRegistered Archive Formats:\n";
    std::cout << "===========================\n";

    UINT32 numFormats = 0;
    HRESULT hr = GetNumberOfFormats(&numFormats);

    if (hr != S_OK) {
        std::cerr << std::format("Error: GetNumberOfFormats failed with code 0x{:08X}\n", hr);
        return;
    }

    std::cout << std::format("Total formats registered: {}\n\n", numFormats);

    for (auto i : std::views::iota(0u, numFormats)) {
        NWindows::NCOM::CPropVariant nameProp, extProp;

        if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kName, &nameProp) == S_OK
            && nameProp.vt == VT_BSTR && nameProp.bstrVal) {

            // Convert wide string to narrow for display
            const wchar_t* wname = static_cast<const wchar_t*>(nameProp.bstrVal);
            std::wcout << std::format(L"[{:2}] {:10}", i, wname);

            if (GetHandlerProperty2(i, NArchive::NHandlerPropID::kExtension, &extProp) == S_OK
                && extProp.vt == VT_BSTR && extProp.bstrVal) {
                const wchar_t* wext = static_cast<const wchar_t*>(extProp.bstrVal);
                std::wcout << std::format(L" Extensions: {}", wext);
            }
            std::wcout << L"\n";
        }
    }
}

int main(int argc, char* argv[]) {
    std::cout << "7z-wrapper Test Program\n";
    std::cout << "=======================\n";

    // First, list all registered formats
    listAllFormats();

    if (argc > 1) {
        // Test files provided as arguments
        auto args = std::span(argv, argc) | std::views::drop(1);
        for (const auto& arg : args) {
            testFile(arg);
        }
    } else {
        // Test with common archive extensions
        std::cout << "\nTesting common archive formats:\n";

        constexpr std::array testFiles = {
            "test.zip",
            "test.7z",
            "test.tar",
            "test.tar.gz",
            "test.tgz",
            "test.rar",
            "test.iso",
            "test.cab",
            "document.docx",  // ZIP-based format
            "test.jar",       // ZIP-based format
            "unknown.xyz",    // Should fail
            "test.exe",       // Should fail (excluded)
            "noextension"     // Should fail
        };

        for (const auto& file : testFiles) {
            testFile(file);
        }
    }

    return 0;
}
