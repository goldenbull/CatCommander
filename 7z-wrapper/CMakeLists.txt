cmake_minimum_required(VERSION 3.10)
project(7zip)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Architecture-specific settings for different platforms
if(WIN32)
  # Windows: Ensure x64 build
  set(CMAKE_GENERATOR_PLATFORM "x64" CACHE STRING "Platform" FORCE)
elseif(APPLE)
  set(CMAKE_OSX_ARCHITECTURES "x86_64;arm64" CACHE STRING "Build for both x86_64 and arm64 architectures")
elseif(UNIX AND NOT APPLE)
   message(STATUS "Building for 64-bit Linux architecture: ${CMAKE_SYSTEM_PROCESSOR}")
endif()

# Paths
set(7Z_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../7z")
set(7Z_C "${7Z_ROOT}/C")
set(7Z_CPP "${7Z_ROOT}/CPP")
set(UNI_ALGO "${CMAKE_CURRENT_SOURCE_DIR}/uni-algo-1.2.0")

# Compiler flags
if(NOT MSVC)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -O2 -Wall -Wextra -D_REENTRANT -D_FILE_OFFSET_BITS=64 -D_LARGEFILE_SOURCE")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -O2 -Wall -Wextra -D_REENTRANT -D_FILE_OFFSET_BITS=64 -D_LARGEFILE_SOURCE")
else()
    add_compile_options(/MP /utf-8)
endif()

# Platform-specific definitions
if(WIN32)
    add_definitions(-DZ7_EXTERNAL_CODECS)
else()
    add_definitions(-DZ7_EXTERNAL_CODECS -DUNICODE -D_UNICODE)
endif()

# Position-independent code for shared libraries
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Include directories
include_directories(
    ${7Z_C}
    ${7Z_CPP}
    ${UNI_ALGO}/include
)

# CPP/Common sources
set(COMMON_SOURCES
    ${7Z_CPP}/Common/CRC.cpp
    ${7Z_CPP}/Common/CrcReg.cpp
    ${7Z_CPP}/Common/DynLimBuf.cpp
    ${7Z_CPP}/Common/IntToString.cpp
    ${7Z_CPP}/Common/LzFindPrepare.cpp
    ${7Z_CPP}/Common/Md5Reg.cpp
    ${7Z_CPP}/Common/MyMap.cpp
    ${7Z_CPP}/Common/MyString.cpp
    ${7Z_CPP}/Common/MyVector.cpp
    ${7Z_CPP}/Common/MyXml.cpp
    ${7Z_CPP}/Common/NewHandler.cpp
    ${7Z_CPP}/Common/Sha1Prepare.cpp
    ${7Z_CPP}/Common/Sha1Reg.cpp
    ${7Z_CPP}/Common/Sha256Prepare.cpp
    ${7Z_CPP}/Common/Sha256Reg.cpp
    ${7Z_CPP}/Common/Sha3Reg.cpp
    ${7Z_CPP}/Common/Sha512Prepare.cpp
    ${7Z_CPP}/Common/Sha512Reg.cpp
    ${7Z_CPP}/Common/StringConvert.cpp
    ${7Z_CPP}/Common/StringToInt.cpp
    ${7Z_CPP}/Common/UTFConvert.cpp
    ${7Z_CPP}/Common/Wildcard.cpp
    ${7Z_CPP}/Common/Xxh64Reg.cpp
    ${7Z_CPP}/Common/XzCrc64Init.cpp
    ${7Z_CPP}/Common/XzCrc64Reg.cpp
)

# CPP/Windows sources
if(WIN32)
    set(WIN_SOURCES
        ${7Z_CPP}/Windows/FileDir.cpp
        ${7Z_CPP}/Windows/FileFind.cpp
        ${7Z_CPP}/Windows/FileIO.cpp
        ${7Z_CPP}/Windows/FileName.cpp
        ${7Z_CPP}/Windows/PropVariant.cpp
        ${7Z_CPP}/Windows/PropVariantConv.cpp
        ${7Z_CPP}/Windows/PropVariantUtils.cpp
        ${7Z_CPP}/Windows/System.cpp
        ${7Z_CPP}/Windows/TimeUtils.cpp
    )
else()
    set(WIN_SOURCES
        ${7Z_CPP}/Windows/FileDir.cpp
        ${7Z_CPP}/Windows/FileFind.cpp
        ${7Z_CPP}/Windows/FileIO.cpp
        ${7Z_CPP}/Windows/FileName.cpp
        ${7Z_CPP}/Windows/PropVariant.cpp
        ${7Z_CPP}/Windows/PropVariantConv.cpp
        ${7Z_CPP}/Windows/PropVariantUtils.cpp
        ${7Z_CPP}/Windows/System.cpp
        ${7Z_CPP}/Windows/TimeUtils.cpp
        ${7Z_CPP}/Common/MyWindows.cpp
    )
endif()

# Multithreading sources (non-ST_MODE)
set(MT_SOURCES
    ${7Z_CPP}/7zip/Common/MemBlocks.cpp
    ${7Z_CPP}/7zip/Common/OutMemStream.cpp
    ${7Z_CPP}/7zip/Common/ProgressMt.cpp
    ${7Z_CPP}/7zip/Common/StreamBinder.cpp
    ${7Z_CPP}/Windows/Synchronization.cpp
    ${7Z_CPP}/7zip/Common/VirtThread.cpp
)

# 7ZIP_COMMON sources
set(7ZIP_COMMON_SOURCES
    ${7Z_CPP}/7zip/Common/CreateCoder.cpp
    ${7Z_CPP}/7zip/Common/CWrappers.cpp
    ${7Z_CPP}/7zip/Common/InBuffer.cpp
    ${7Z_CPP}/7zip/Common/InOutTempBuffer.cpp
    ${7Z_CPP}/7zip/Common/FilterCoder.cpp
    ${7Z_CPP}/7zip/Common/LimitedStreams.cpp
    ${7Z_CPP}/7zip/Common/LockedStream.cpp
    ${7Z_CPP}/7zip/Common/MethodId.cpp
    ${7Z_CPP}/7zip/Common/MethodProps.cpp
    ${7Z_CPP}/7zip/Common/OffsetStream.cpp
    ${7Z_CPP}/7zip/Common/OutBuffer.cpp
    ${7Z_CPP}/7zip/Common/ProgressUtils.cpp
    ${7Z_CPP}/7zip/Common/PropId.cpp
    ${7Z_CPP}/7zip/Common/StreamObjects.cpp
    ${7Z_CPP}/7zip/Common/StreamUtils.cpp
    ${7Z_CPP}/7zip/Common/UniqBlocks.cpp
)

# Archive handlers
set(AR_SOURCES
    ${7Z_CPP}/7zip/Archive/ApfsHandler.cpp
    ${7Z_CPP}/7zip/Archive/ApmHandler.cpp
    ${7Z_CPP}/7zip/Archive/ArHandler.cpp
    ${7Z_CPP}/7zip/Archive/ArjHandler.cpp
    ${7Z_CPP}/7zip/Archive/Base64Handler.cpp
    ${7Z_CPP}/7zip/Archive/Bz2Handler.cpp
    ${7Z_CPP}/7zip/Archive/ComHandler.cpp
    ${7Z_CPP}/7zip/Archive/CpioHandler.cpp
    ${7Z_CPP}/7zip/Archive/CramfsHandler.cpp
    ${7Z_CPP}/7zip/Archive/DeflateProps.cpp
    ${7Z_CPP}/7zip/Archive/DmgHandler.cpp
    ${7Z_CPP}/7zip/Archive/ElfHandler.cpp
    ${7Z_CPP}/7zip/Archive/ExtHandler.cpp
    ${7Z_CPP}/7zip/Archive/FatHandler.cpp
    ${7Z_CPP}/7zip/Archive/FlvHandler.cpp
    ${7Z_CPP}/7zip/Archive/GzHandler.cpp
    ${7Z_CPP}/7zip/Archive/GptHandler.cpp
    ${7Z_CPP}/7zip/Archive/HandlerCont.cpp
    ${7Z_CPP}/7zip/Archive/HfsHandler.cpp
    ${7Z_CPP}/7zip/Archive/IhexHandler.cpp
    ${7Z_CPP}/7zip/Archive/LpHandler.cpp
    ${7Z_CPP}/7zip/Archive/LzhHandler.cpp
    ${7Z_CPP}/7zip/Archive/LzmaHandler.cpp
    ${7Z_CPP}/7zip/Archive/MachoHandler.cpp
    ${7Z_CPP}/7zip/Archive/MbrHandler.cpp
    ${7Z_CPP}/7zip/Archive/MslzHandler.cpp
    ${7Z_CPP}/7zip/Archive/MubHandler.cpp
    ${7Z_CPP}/7zip/Archive/NtfsHandler.cpp
    ${7Z_CPP}/7zip/Archive/PeHandler.cpp
    ${7Z_CPP}/7zip/Archive/PpmdHandler.cpp
    ${7Z_CPP}/7zip/Archive/QcowHandler.cpp
    ${7Z_CPP}/7zip/Archive/RpmHandler.cpp
    ${7Z_CPP}/7zip/Archive/SparseHandler.cpp
    ${7Z_CPP}/7zip/Archive/SplitHandler.cpp
    ${7Z_CPP}/7zip/Archive/SquashfsHandler.cpp
    ${7Z_CPP}/7zip/Archive/SwfHandler.cpp
    ${7Z_CPP}/7zip/Archive/UefiHandler.cpp
    ${7Z_CPP}/7zip/Archive/VdiHandler.cpp
    ${7Z_CPP}/7zip/Archive/VhdHandler.cpp
    ${7Z_CPP}/7zip/Archive/VhdxHandler.cpp
    ${7Z_CPP}/7zip/Archive/VmdkHandler.cpp
    ${7Z_CPP}/7zip/Archive/XarHandler.cpp
    ${7Z_CPP}/7zip/Archive/XzHandler.cpp
    ${7Z_CPP}/7zip/Archive/ZHandler.cpp
    ${7Z_CPP}/7zip/Archive/ZstdHandler.cpp
)

# Archive common
set(AR_COMMON_SOURCES
    ${7Z_CPP}/7zip/Archive/Common/CoderMixer2.cpp
    ${7Z_CPP}/7zip/Archive/Common/DummyOutStream.cpp
    ${7Z_CPP}/7zip/Archive/Common/FindSignature.cpp
    ${7Z_CPP}/7zip/Archive/Common/InStreamWithCRC.cpp
    ${7Z_CPP}/7zip/Archive/Common/ItemNameUtils.cpp
    ${7Z_CPP}/7zip/Archive/Common/MultiStream.cpp
    ${7Z_CPP}/7zip/Archive/Common/OutStreamWithCRC.cpp
    ${7Z_CPP}/7zip/Archive/Common/OutStreamWithSha1.cpp
    ${7Z_CPP}/7zip/Archive/Common/HandlerOut.cpp
    ${7Z_CPP}/7zip/Archive/Common/ParseProperties.cpp
)

# 7z format
set(7Z_SOURCES
    ${7Z_CPP}/7zip/Archive/7z/7zCompressionMode.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zDecode.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zEncode.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zExtract.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zFolderInStream.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zHandler.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zHandlerOut.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zHeader.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zIn.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zOut.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zProperties.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zSpecStream.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zUpdate.cpp
    ${7Z_CPP}/7zip/Archive/7z/7zRegister.cpp
)

# CAB format
set(CAB_SOURCES
    ${7Z_CPP}/7zip/Archive/Cab/CabBlockInStream.cpp
    ${7Z_CPP}/7zip/Archive/Cab/CabHandler.cpp
    ${7Z_CPP}/7zip/Archive/Cab/CabHeader.cpp
    ${7Z_CPP}/7zip/Archive/Cab/CabIn.cpp
    ${7Z_CPP}/7zip/Archive/Cab/CabRegister.cpp
)

# CHM format
set(CHM_SOURCES
    ${7Z_CPP}/7zip/Archive/Chm/ChmHandler.cpp
    ${7Z_CPP}/7zip/Archive/Chm/ChmIn.cpp
)

# ISO format
set(ISO_SOURCES
    ${7Z_CPP}/7zip/Archive/Iso/IsoHandler.cpp
    ${7Z_CPP}/7zip/Archive/Iso/IsoHeader.cpp
    ${7Z_CPP}/7zip/Archive/Iso/IsoIn.cpp
    ${7Z_CPP}/7zip/Archive/Iso/IsoRegister.cpp
)

# NSIS format
set(NSIS_SOURCES
    ${7Z_CPP}/7zip/Archive/Nsis/NsisDecode.cpp
    ${7Z_CPP}/7zip/Archive/Nsis/NsisHandler.cpp
    ${7Z_CPP}/7zip/Archive/Nsis/NsisIn.cpp
    ${7Z_CPP}/7zip/Archive/Nsis/NsisRegister.cpp
)

# RAR format
set(RAR_SOURCES
    ${7Z_CPP}/7zip/Archive/Rar/RarHandler.cpp
    ${7Z_CPP}/7zip/Archive/Rar/Rar5Handler.cpp
)

# TAR format
set(TAR_SOURCES
    ${7Z_CPP}/7zip/Archive/Tar/TarHandler.cpp
    ${7Z_CPP}/7zip/Archive/Tar/TarHandlerOut.cpp
    ${7Z_CPP}/7zip/Archive/Tar/TarHeader.cpp
    ${7Z_CPP}/7zip/Archive/Tar/TarIn.cpp
    ${7Z_CPP}/7zip/Archive/Tar/TarOut.cpp
    ${7Z_CPP}/7zip/Archive/Tar/TarUpdate.cpp
    ${7Z_CPP}/7zip/Archive/Tar/TarRegister.cpp
)

# UDF format
set(UDF_SOURCES
    ${7Z_CPP}/7zip/Archive/Udf/UdfHandler.cpp
    ${7Z_CPP}/7zip/Archive/Udf/UdfIn.cpp
)

# WIM format
set(WIM_SOURCES
    ${7Z_CPP}/7zip/Archive/Wim/WimHandler.cpp
    ${7Z_CPP}/7zip/Archive/Wim/WimHandlerOut.cpp
    ${7Z_CPP}/7zip/Archive/Wim/WimIn.cpp
    ${7Z_CPP}/7zip/Archive/Wim/WimRegister.cpp
)

# ZIP format
set(ZIP_SOURCES
    ${7Z_CPP}/7zip/Archive/Zip/ZipAddCommon.cpp
    ${7Z_CPP}/7zip/Archive/Zip/ZipHandler.cpp
    ${7Z_CPP}/7zip/Archive/Zip/ZipHandlerOut.cpp
    ${7Z_CPP}/7zip/Archive/Zip/ZipIn.cpp
    ${7Z_CPP}/7zip/Archive/Zip/ZipItem.cpp
    ${7Z_CPP}/7zip/Archive/Zip/ZipOut.cpp
    ${7Z_CPP}/7zip/Archive/Zip/ZipUpdate.cpp
    ${7Z_CPP}/7zip/Archive/Zip/ZipRegister.cpp
)

# Compress codecs
set(COMPRESS_SOURCES
    ${7Z_CPP}/7zip/Compress/Bcj2Coder.cpp
    ${7Z_CPP}/7zip/Compress/Bcj2Register.cpp
    ${7Z_CPP}/7zip/Compress/BcjCoder.cpp
    ${7Z_CPP}/7zip/Compress/BcjRegister.cpp
    ${7Z_CPP}/7zip/Compress/BitlDecoder.cpp
    ${7Z_CPP}/7zip/Compress/BranchMisc.cpp
    ${7Z_CPP}/7zip/Compress/BranchRegister.cpp
    ${7Z_CPP}/7zip/Compress/ByteSwap.cpp
    ${7Z_CPP}/7zip/Compress/BZip2Crc.cpp
    ${7Z_CPP}/7zip/Compress/BZip2Decoder.cpp
    ${7Z_CPP}/7zip/Compress/BZip2Encoder.cpp
    ${7Z_CPP}/7zip/Compress/BZip2Register.cpp
    ${7Z_CPP}/7zip/Compress/CopyCoder.cpp
    ${7Z_CPP}/7zip/Compress/CopyRegister.cpp
    ${7Z_CPP}/7zip/Compress/Deflate64Register.cpp
    ${7Z_CPP}/7zip/Compress/DeflateDecoder.cpp
    ${7Z_CPP}/7zip/Compress/DeflateEncoder.cpp
    ${7Z_CPP}/7zip/Compress/DeflateRegister.cpp
    ${7Z_CPP}/7zip/Compress/DeltaFilter.cpp
    ${7Z_CPP}/7zip/Compress/ImplodeDecoder.cpp
    ${7Z_CPP}/7zip/Compress/LzfseDecoder.cpp
    ${7Z_CPP}/7zip/Compress/LzhDecoder.cpp
    ${7Z_CPP}/7zip/Compress/Lzma2Decoder.cpp
    ${7Z_CPP}/7zip/Compress/Lzma2Encoder.cpp
    ${7Z_CPP}/7zip/Compress/Lzma2Register.cpp
    ${7Z_CPP}/7zip/Compress/LzmaDecoder.cpp
    ${7Z_CPP}/7zip/Compress/LzmaEncoder.cpp
    ${7Z_CPP}/7zip/Compress/LzmaRegister.cpp
    ${7Z_CPP}/7zip/Compress/LzmsDecoder.cpp
    ${7Z_CPP}/7zip/Compress/LzOutWindow.cpp
    ${7Z_CPP}/7zip/Compress/LzxDecoder.cpp
    ${7Z_CPP}/7zip/Compress/PpmdDecoder.cpp
    ${7Z_CPP}/7zip/Compress/PpmdEncoder.cpp
    ${7Z_CPP}/7zip/Compress/PpmdRegister.cpp
    ${7Z_CPP}/7zip/Compress/PpmdZip.cpp
    ${7Z_CPP}/7zip/Compress/QuantumDecoder.cpp
    ${7Z_CPP}/7zip/Compress/ShrinkDecoder.cpp
    ${7Z_CPP}/7zip/Compress/XpressDecoder.cpp
    ${7Z_CPP}/7zip/Compress/XzDecoder.cpp
    ${7Z_CPP}/7zip/Compress/XzEncoder.cpp
    ${7Z_CPP}/7zip/Compress/ZlibDecoder.cpp
    ${7Z_CPP}/7zip/Compress/ZlibEncoder.cpp
    ${7Z_CPP}/7zip/Compress/ZDecoder.cpp
    ${7Z_CPP}/7zip/Compress/ZstdDecoder.cpp
    ${7Z_CPP}/7zip/Compress/Rar1Decoder.cpp
    ${7Z_CPP}/7zip/Compress/Rar2Decoder.cpp
    ${7Z_CPP}/7zip/Compress/Rar3Decoder.cpp
    ${7Z_CPP}/7zip/Compress/Rar3Vm.cpp
    ${7Z_CPP}/7zip/Compress/Rar5Decoder.cpp
    ${7Z_CPP}/7zip/Compress/RarCodecsRegister.cpp
)

# Crypto
set(CRYPTO_SOURCES
    ${7Z_CPP}/7zip/Crypto/7zAes.cpp
    ${7Z_CPP}/7zip/Crypto/7zAesRegister.cpp
    ${7Z_CPP}/7zip/Crypto/HmacSha1.cpp
    ${7Z_CPP}/7zip/Crypto/HmacSha256.cpp
    ${7Z_CPP}/7zip/Crypto/MyAes.cpp
    ${7Z_CPP}/7zip/Crypto/MyAesReg.cpp
    ${7Z_CPP}/7zip/Crypto/Pbkdf2HmacSha1.cpp
    ${7Z_CPP}/7zip/Crypto/RandGen.cpp
    ${7Z_CPP}/7zip/Crypto/WzAes.cpp
    ${7Z_CPP}/7zip/Crypto/ZipCrypto.cpp
    ${7Z_CPP}/7zip/Crypto/ZipStrong.cpp
    ${7Z_CPP}/7zip/Crypto/Rar20Crypto.cpp
    ${7Z_CPP}/7zip/Crypto/Rar5Aes.cpp
    ${7Z_CPP}/7zip/Crypto/RarAes.cpp
)

# C sources
set(C_SOURCES
    ${7Z_C}/7zBuf2.c
    ${7Z_C}/7zCrc.c
    ${7Z_C}/7zCrcOpt.c
    ${7Z_C}/7zStream.c
    ${7Z_C}/Aes.c
    ${7Z_C}/AesOpt.c
    ${7Z_C}/Alloc.c
    ${7Z_C}/Bcj2.c
    ${7Z_C}/Bcj2Enc.c
    ${7Z_C}/Blake2s.c
    ${7Z_C}/Bra.c
    ${7Z_C}/Bra86.c
    ${7Z_C}/BraIA64.c
    ${7Z_C}/BwtSort.c
    ${7Z_C}/CpuArch.c
    ${7Z_C}/Delta.c
    ${7Z_C}/HuffEnc.c
    ${7Z_C}/LzFind.c
    ${7Z_C}/LzFindMt.c
    ${7Z_C}/Lzma2Dec.c
    ${7Z_C}/Lzma2DecMt.c
    ${7Z_C}/Lzma2Enc.c
    ${7Z_C}/LzmaDec.c
    ${7Z_C}/LzmaEnc.c
    ${7Z_C}/Md5.c
    ${7Z_C}/MtCoder.c
    ${7Z_C}/MtDec.c
    ${7Z_C}/Ppmd7.c
    ${7Z_C}/Ppmd7Dec.c
    ${7Z_C}/Ppmd7aDec.c
    ${7Z_C}/Ppmd7Enc.c
    ${7Z_C}/Ppmd8.c
    ${7Z_C}/Ppmd8Dec.c
    ${7Z_C}/Ppmd8Enc.c
    ${7Z_C}/Sha1.c
    ${7Z_C}/Sha1Opt.c
    ${7Z_C}/Sha256.c
    ${7Z_C}/Sha256Opt.c
    ${7Z_C}/Sha3.c
    ${7Z_C}/Sha512.c
    ${7Z_C}/Sha512Opt.c
    ${7Z_C}/Sort.c
    ${7Z_C}/SwapBytes.c
    ${7Z_C}/Threads.c
    ${7Z_C}/Xxh64.c
    ${7Z_C}/Xz.c
    ${7Z_C}/XzDec.c
    ${7Z_C}/XzEnc.c
    ${7Z_C}/XzIn.c
    ${7Z_C}/XzCrc64.c
    ${7Z_C}/XzCrc64Opt.c
    ${7Z_C}/ZstdDec.c
    ${7Z_C}/LzFindOpt.c
)

# Export sources for DLL
set(EXPORT_SOURCES
    ${7Z_CPP}/7zip/Archive/ArchiveExports.cpp
    ${7Z_CPP}/7zip/Archive/DllExports2.cpp
    ${7Z_CPP}/7zip/Compress/CodecExports.cpp
)

# Combine all sources
set(ALL_SOURCES
    ${C_SOURCES}
    ${COMMON_SOURCES}
    ${WIN_SOURCES}
    ${MT_SOURCES}
    ${7ZIP_COMMON_SOURCES}
    ${AR_SOURCES}
    ${AR_COMMON_SOURCES}
    ${7Z_SOURCES}
    ${CAB_SOURCES}
    ${CHM_SOURCES}
    ${ISO_SOURCES}
    ${NSIS_SOURCES}
    ${RAR_SOURCES}
    ${TAR_SOURCES}
    ${UDF_SOURCES}
    ${WIM_SOURCES}
    ${ZIP_SOURCES}
    ${COMPRESS_SOURCES}
    ${CRYPTO_SOURCES}
    ${EXPORT_SOURCES}
)

add_subdirectory(${UNI_ALGO})

# Create shared library
if(WIN32)
    # On Windows, use the custom .def file for exports (includes 7-Zip and custom exports)
    add_library(7z SHARED ${ALL_SOURCES} ${CMAKE_CURRENT_SOURCE_DIR}/wrapper.cpp ${CMAKE_CURRENT_SOURCE_DIR}/ArchiveInfoManager.cpp ${CMAKE_CURRENT_SOURCE_DIR}/wrapper.def)
else()
    add_library(7z SHARED ${ALL_SOURCES} ${CMAKE_CURRENT_SOURCE_DIR}/wrapper.cpp ${CMAKE_CURRENT_SOURCE_DIR}/ArchiveInfoManager.cpp)
endif()

# Link libraries
if(WIN32)
    target_link_libraries(7z
        oleaut32
        ole32
        user32
        advapi32
        shell32
        uni-algo::uni-algo
    )
else()
    target_link_libraries(7z
        pthread
        dl
        uni-algo::uni-algo
    )
endif()

# Set output properties
if(WIN32)
    set_target_properties(7z PROPERTIES PREFIX "" SUFFIX ".dll")
elseif(APPLE)
    set_target_properties(7z PROPERTIES PREFIX "" SUFFIX ".dylib")
else()
    set_target_properties(7z PROPERTIES PREFIX "" SUFFIX ".so")
endif()

# Set output directory to 7z.net/lib
set_target_properties(7z PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../7z.net/lib"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../7z.net/lib"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../7z.net/lib"
)

# Install targets - Use relative path to avoid CMP0177 policy warning
install(TARGETS 7z
    LIBRARY DESTINATION ../7z.net/lib
    ARCHIVE DESTINATION ../7z.net/lib
    RUNTIME DESTINATION ../7z.net/lib
)

# Test executable
add_executable(7z-test ${CMAKE_CURRENT_SOURCE_DIR}/main.cpp)
target_link_libraries(7z-test 7z)

# Set output directory for test executable
set_target_properties(7z-test PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
)

# Print configuration summary
message(STATUS "7-Zip Configuration:")
message(STATUS "  Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "  Platform: ${CMAKE_SYSTEM_NAME}")
message(STATUS "  C Compiler: ${CMAKE_C_COMPILER}")
message(STATUS "  CXX Compiler: ${CMAKE_CXX_COMPILER}")
