#pragma once

#include "ArchiveInfoManager.h"

extern "C" {
bool GetArchiveInfoByFilename(const wchar_t *fname, ArchiveInfo &info);
}
