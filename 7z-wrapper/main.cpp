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

#include "wrapper.h"

void testFile(std::wstring_view filename) {
    std::wcout << std::format(L"\nTesting: {}\n", filename);
    std::wcout << L"=====================================\n";

    //ArchiveInfo info;
    //if (GetArchiveInfoByFilename(filename.data(), info)) {
    //    std::wcout << L"Format detected!\n";
    //    std::wcout << std::format(L"  Name:      {}\n", info.Name.IsEmpty() ? L"(null)" : info.Name.Ptr());
    //    std::wcout << std::format(L"  Ext:       {}\n", info.Ext.IsEmpty() ? L"(null)" : info.Ext.Ptr());
    //    std::wcout << std::format(L"  AddExt:    {}\n", info.AddExt.IsEmpty() ? L"(null)" : info.AddExt.Ptr());
    //    std::wcout << std::format(L"  Flags:     0x{:08X}\n", info.Flags);
    //    std::wcout << std::format(L"  TimeFlags: 0x{:08X}\n", info.TimeFlags);
    //} else {
    //    std::wcout << L"No format detected (unknown or unsupported)\n";
    //}
}

int main(int argc, char *argv[]) {
    std::wcout << L"7z-wrapper Test Program\n";
    std::wcout << L"=======================\n";

    // First, list all registered formats
    std::wcout << L"\nRegistered Archive Formats:\n";
    std::wcout << L"===========================\n";
    //ListAllFormats();

    if (argc > 1) {
        // Test files provided as arguments
        auto args = std::span(argv, argc) | std::views::drop(1);
        for (const auto &arg: args) {
            std::string s(arg);
            std::wstring ws(s.begin(), s.end());
            testFile(ws);
        }
    } else {
        // Test with common archive extensions
        std::wcout << L"\nTesting common archive formats:\n";

        std::vector<std::wstring> testFiles = {
            L"test.zip",
            L"test.7z",
            L"test.tar",
            L"test.tar.gz",
            L"test.tgz",
            L"test.rar",
            L"test.iso",
            L"test.cab",
            L"document.docx", // ZIP-based format
            L"test.jar", // ZIP-based format
            L"unknown.xyz", // Should fail
            L"test.exe", // Should fail (excluded)
            L"noextension" // Should fail
        };

        for (const auto &file: testFiles) {
            testFile(file);
            std::wcout << file << L"\n";
        }
    }

    return 0;
}
