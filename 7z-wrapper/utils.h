#pragma once

#include <algorithm>
#include <string>

inline std::string tolower(const std::string &s) {
    std::string r(s);
    std::transform(s.begin(), s.end(), r.begin(), [](char c) { return std::tolower(c); });
    return r;
}

inline std::string tolower(const std::string_view &s) {
    return tolower(std::string(s));
}

inline std::wstring tolower(const std::wstring &s) {
    std::wstring r(s);
    std::transform(s.begin(), s.end(), r.begin(), [](wchar_t c) { return std::tolower(c); });
    return r;
}

inline std::wstring tolower(const std::wstring_view &s) {
    return tolower(std::wstring(s));
}
