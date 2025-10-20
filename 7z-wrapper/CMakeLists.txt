cmake_minimum_required(VERSION 3.20)
project(7z-wrapper VERSION 1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Output directory
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# Platform detection
if(WIN32)
    set(IS_MINGW TRUE)
else()
    set(IS_MINGW FALSE)
endif()

# 7-Zip source paths relative to this CMakeLists.txt
set(7Z_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../7z")
set(7Z_CPP "${7Z_ROOT}/CPP")
set(7Z_C "${7Z_ROOT}/C")

# Common include directories
include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${7Z_CPP}
    ${7Z_CPP}/Common
    ${7Z_CPP}/Windows
    ${7Z_CPP}/7zip
    ${7Z_CPP}/7zip/Common
    ${7Z_CPP}/7zip/Archive
    ${7Z_CPP}/7zip/Archive/Common
    ${7Z_CPP}/7zip/Archive/7z
    ${7Z_CPP}/7zip/Archive/Cab
    ${7Z_CPP}/7zip/Archive/Chm
    ${7Z_CPP}/7zip/Archive/Iso
    ${7Z_CPP}/7zip/Archive/Nsis
    ${7Z_CPP}/7zip/Archive/Rar
    ${7Z_CPP}/7zip/Archive/Tar
    ${7Z_CPP}/7zip/Archive/Udf
    ${7Z_CPP}/7zip/Archive/Wim
    ${7Z_CPP}/7zip/Archive/Zip
    ${7Z_CPP}/7zip/Compress
    ${7Z_CPP}/7zip/Crypto
    ${7Z_C}
)

# Compiler flags
add_definitions(
    -DZ7_EXTERNAL_CODECS
    -D_REENTRANT
    -D_FILE_OFFSET_BITS=64
    -D_LARGEFILE_SOURCE
)

if(NOT WIN32)
    add_definitions(-DUNICODE -D_UNICODE)
endif()

# Optimization flags
if(CMAKE_BUILD_TYPE STREQUAL "Release")
    add_definitions(-DNDEBUG)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -O2")
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -O2")
else()
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -g")
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -g")
endif()

# Position-independent code for shared libraries
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Warning flags
if(NOT MSVC)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -Wextra")
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -Wall -Wextra")
endif()

# C_SOURCE
file(GLOB C_SOURCE "${7Z_C}/*.c")

# CPP/Common
file(GLOB_RECURSE COMMON_SOURCES "${7Z_CPP}/Common/*.cpp")

if(NOT IS_MINGW)
    list(REMOVE_ITEM COMMON_SOURCES ${7Z_CPP}/Common/MyWindows.cpp)
endif()

# CPP/Windows
file(GLOB_RECURSE WIN_SOURCES "${7Z_CPP}/Windows/*.cpp")

# 7ZIP_COMMON_OBJS
file(GLOB_RECURSE 7ZIP_COMMON_SOURCES "${7Z_CPP}/Common/*.cpp")

# AR_OBJS (Archive handlers)
file(GLOB_RECURSE AR_SOURCES "${7Z_CPP}/7zip/Archive/*.cpp")
if(NOT IS_MINGW)
    list(REMOVE_ITEM AR_SOURCES "${7Z_CPP}/7zip/Archive/DllExports.cpp")
    list(REMOVE_ITEM AR_SOURCES "${7Z_CPP}/7zip/Archive/DllExports2.cpp")
endif()

# COMPRESS_OBJS
file(GLOB_RECURSE COMPRESS_SOURCES "${7Z_CPP}/7zip/Compress/*.cpp")
if(NOT IS_MINGW)
    list(REMOVE_ITEM COMPRESS_SOURCES "${7Z_CPP}/7zip/Compress/DllExports2Compress.cpp")
endif()

# CRYPTO_OBJS
file(GLOB_RECURSE CRYPTO_SOURCES "${7Z_CPP}/7zip/Crypto/*.cpp")

# Export functions for DLL/shared library
# set(ARCHIVE_EXPORTS_SOURCES
#     ${7Z_CPP}/7zip/Archive/ArchiveExports.cpp
#     ${7Z_CPP}/7zip/Archive/DllExports2.cpp
# )

# set(CODEC_EXPORTS_SOURCES
#     ${7Z_CPP}/7zip/Compress/CodecExports.cpp
# )

# Create precompiled header source
# file(WRITE ${CMAKE_CURRENT_BINARY_DIR}/StdAfx.cpp "#include \"StdAfx.h\"\n")

# Wrapper sources
# set(WRAPPER_SOURCES
#     ${CMAKE_CURRENT_SOURCE_DIR}/src/Sz7zCWrapper.cpp
#     ${CMAKE_CURRENT_BINARY_DIR}/StdAfx.cpp
# )

# Create StdAfx.h
# file(WRITE ${CMAKE_CURRENT_BINARY_DIR}/StdAfx.h
# "#ifndef STDAFX_H
# #define STDAFX_H

# #ifdef _WIN32
# #include <windows.h>
# #endif

# #include \"Common/Common.h\"
# #include \"Common/MyWindows.h\"

# #endif
# ")

include_directories(${CMAKE_CURRENT_BINARY_DIR})

# Combine all sources
set(ALL_SOURCES
    # ${WRAPPER_SOURCES}
    ${COMMON_SOURCES}
    # ${WIN_SOURCES}
    ${7ZIP_COMMON_SOURCES}
    ${AR_SOURCES}
    ${COMPRESS_SOURCES}
    ${CRYPTO_SOURCES}
    ${C_SOURCES}
    # ${ARCHIVE_EXPORTS_SOURCES}
    # ${CODEC_EXPORTS_SOURCES}
)

# Create shared library
add_library(7z-wrapper SHARED ${ALL_SOURCES})

# Link libraries
if(WIN32)
    target_link_libraries(7z-wrapper
        oleaut32
        ole32
        user32
        advapi32
        shell32
    )
else()
    target_link_libraries(7z-wrapper
        pthread
        dl
    )
endif()

# Set output name
if(WIN32)
    set_target_properties(7z-wrapper PROPERTIES
        OUTPUT_NAME "7z"
        PREFIX ""
        SUFFIX ".dll"
    )
else()
    set_target_properties(7z-wrapper PROPERTIES
        OUTPUT_NAME "7z"
        PREFIX "lib"
        SUFFIX ".so"
    )
endif()

# Install targets
install(TARGETS 7z-wrapper
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
    RUNTIME DESTINATION bin
)

# install(FILES ${CMAKE_CURRENT_SOURCE_DIR}/include/Sz7zCWrapper.h
#     DESTINATION include
# )

# Print configuration summary
message(STATUS "7z-wrapper Configuration:")
message(STATUS "  Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "  Platform: ${CMAKE_SYSTEM_NAME}")
message(STATUS "  C Compiler: ${CMAKE_C_COMPILER}")
message(STATUS "  CXX Compiler: ${CMAKE_CXX_COMPILER}")
message(STATUS "  Output directory: ${CMAKE_LIBRARY_OUTPUT_DIRECTORY}")
