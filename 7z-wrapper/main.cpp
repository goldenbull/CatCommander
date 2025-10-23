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

#include "uni_algo/conv.h"
#include "wrapper.h"

int main(int argc, char *argv[])
{
    // First, list all registered formats
    std::wcout << L"\nRegistered Archive Formats:\n";
    std::wcout << L"===========================\n";
    std::u16string formats = GetAllFormatNames();
    for (auto view : std::views::split(formats, ' ')) // TODO: can not use string as delimiter, why?
    {
        auto fmt = std::u16string(std::u16string_view(view));
        std::cout << una::utf16to8(fmt) << ":" << std::endl;
        FormatInfo info;
        if (!GetFormatInfoByName(fmt.data(), &info))
        {
            std::cout << "can not get format info of " << una::utf16to8(fmt) << std::endl;
            continue;
        }
        else
        {
            std::cout << std::format("  Ext:       {}\n", una::utf16to8(info.Ext));
            std::cout << std::format("  AddExt:    {}\n", una::utf16to8(info.AddExt));
        }
    }

    std::u16string filename = u"/Users/cjn/Downloads/uni-algo-1.2.0.zip";
    if (argc > 1)
    {
        filename = una::utf8to16<char, char16_t>(argv[1]);
    }
    auto ret = TestExpandToCurrentFolder(filename.data());
    std::cout << "TestExpandToCurrentFolder returns " << ret << std::endl;

    return 0;
}
