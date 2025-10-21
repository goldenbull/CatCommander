#pragma once

#include <algorithm>
#include <string>

inline std::string tolower(const std::string &s) {
    std::string r(s);
    std::transform(s.begin(), s.end(), r.begin(), [](unsigned char c) { return std::tolower(c); });
    return r;
}

inline std::string tolower(const std::string_view &s) {
    return tolower(std::string(s));
}
