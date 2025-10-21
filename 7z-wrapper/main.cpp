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
#include "7zip/Archive/IArchive.h"

#include "wrapper.h"
#include "ArchiveInfoManager.h"
#include "Windows/PropVariant.h"

void testFile(std::string_view filename) {
    std::cout << std::format("\nTesting: {}\n", filename);
    std::cout << "=====================================\n";

    ArchiveInfo info;

    if (GetArchiveInfo(filename.data(), &info)) {
        std::cout << "✓ Format detected!\n";
        std::cout << std::format("  Name:      {}\n",
                                 info.Name.IsEmpty() ? "(null)" : info.Name.Ptr());
        std::cout << std::format("  Ext:       {}\n",
                                 info.Ext.IsEmpty() ? "(null)" : info.Ext.Ptr());
        std::cout << std::format("  AddExt:    {}\n",
                                 info.AddExt.IsEmpty() ? "(null)" : info.AddExt.Ptr());
        std::cout << std::format("  Flags:     0x{:08X}\n", info.Flags);
        std::cout << std::format("  TimeFlags: 0x{:08X}\n", info.TimeFlags);
    } else {
        std::cout << "✗ No format detected (unknown or unsupported)\n";
    }
}

void listAllFormats() {
    std::cout << "\nRegistered Archive Formats:\n";
    std::cout << "===========================\n";

    auto &mgr = ArchiveInfoManager::getInstance();
    auto names = mgr.getAllFormatNames();

    std::cout << std::format("Total formats registered: {}\n\n", names.size());

    for (auto i: std::views::iota(0u, names.size())) {
        ArchiveInfo info;
        mgr.getArchiveInfoByName(names[i], info);
        std::cout << std::format("[{:2}] {:10}", i, info.Name.Ptr());
        std::cout << std::format(" Extensions: {}", info.Ext.Ptr());
        std::cout << "\n";
    }
}

int main(int argc, char *argv[]) {
    std::cout << "7z-wrapper Test Program\n";
    std::cout << "=======================\n";

    // First, list all registered formats
    // Test ArchiveInfoManager directly
    listAllFormats();

    if (argc > 1) {
        // Test files provided as arguments
        auto args = std::span(argv, argc) | std::views::drop(1);
        for (const auto &arg: args) {
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
            "document.docx", // ZIP-based format
            "test.jar", // ZIP-based format
            "unknown.xyz", // Should fail
            "test.exe", // Should fail (excluded)
            "noextension" // Should fail
        };

        for (const auto &file: testFiles) {
            testFile(file);
        }
    }

    return 0;
}
