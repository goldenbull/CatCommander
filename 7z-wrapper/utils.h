#pragma once

#include <algorithm>
#include <string>

inline std::string strlower(const std::string& s)
{
    std::string r(s);
    std::ranges::transform(s, r.begin(), [](const char c) { return std::tolower(c); });
    return r;
}

inline std::string strlower(const std::string_view& s)
{
    return strlower(std::string(s));
}

inline std::wstring strlower(const std::wstring& s)
{
    std::wstring r(s);
    std::ranges::transform(s, r.begin(), [](const wchar_t c) { return std::tolower(c); });
    return r;
}

inline std::wstring strlower(const std::wstring_view& s)
{
    return strlower(std::wstring(s));
}
