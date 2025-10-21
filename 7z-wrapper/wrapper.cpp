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

#include "7zip/Common/FileStreams.h"
#include "7zip/Archive/IArchive.h"
#include "7zip/IPassword.h"

#include "utils.h"
#include "ArchiveInfoManager.h"
#include "wrapper.h"

bool GetArchiveInfo(const char *fname, ArchiveInfo *outInfo) {
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
        return false; // No extension
    }

    // Don't handle .exe files
    if (tolower(extStr) == "exe") {
        return false;
    }

    // Use the singleton to get archive info by extension
    ArchiveInfoManager &manager = ArchiveInfoManager::getInstance();
    if (!manager.getArchiveInfoByExtension(extStr, *outInfo)) {
        return false; // No matching format found
    }

    return true;
}
