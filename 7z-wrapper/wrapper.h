#pragma once

#include "ArchiveInfoManager.h"

extern "C"
{
    bool GetArchiveInfo(const char* fname, ArchiveInfo* info);
}
