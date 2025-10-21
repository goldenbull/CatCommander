#pragma once

#include "7zip/Common/RegisterArc.h"

extern "C"
{
    bool GetArchiveInfo(const char* fname, CArcInfo* info);
}
