# 7-Zip C Wrapper for C# Interop

This wrapper provides a simple C API layer on top of the 7-Zip COM interface, making it much easier to use from C# via P/Invoke.

## Directory Structure

```
7z-wrapper/
├── include/
│   └── Sz7zCWrapper.h         # C API header file
├── src/
│   └── Sz7zCWrapper.cpp       # C++ implementation (wraps COM)
├── build/
│   ├── makefile               # Windows nmake build file
│   ├── makefile.gcc           # Unix/Linux/macOS build file
│   └── Archive2Wrapper.def    # DLL exports definition
└── README.md                  # This file
```

**Important**: The wrapper is kept separate from the `7z/` source code directory to make upgrading 7-Zip easier. When you update the 7-Zip source, you don't need to modify these wrapper files.

## Quick Start

### Build on Windows

```cmd
cd 7z-wrapper\build
build.cmd
```

### Build on Linux/macOS

```bash
cd 7z-wrapper/build
./build.sh
```

The build scripts will:
1. Generate a temporary makefile in the 7z source directory
2. Build the library with wrapper additions
3. Copy the result back to `7z-wrapper/build/`

### Use in C#

```csharp
using CatCommander.SevenZip;

using var archive = new SevenZipArchive("test.7z");
archive.ExtractAll("output");
```

See full documentation below for detailed usage examples.

---

## Full Documentation

For complete API reference, usage examples, and troubleshooting, see the sections below.

### C# Wrapper Classes

Located in `CatCommander/SevenZip/`:
- `SevenZipNative.cs` - Low-level P/Invoke
- `SevenZipArchive.cs` - High-level managed API

### Example: List Archive Contents

```csharp
using var archive = new SevenZipArchive("archive.7z");
foreach (var item in archive.GetAllItems())
{
    Console.WriteLine($"{item.Path} ({item.Size:N0} bytes)");
}
```

### Example: Extract with Progress

```csharp
archive.ExtractAll("output", (total, completed) =>
{
    var percent = completed * 100 / total;
    Console.WriteLine($"Extracting: {percent}%");
});
```

### Upgrading 7-Zip

When upgrading to a new 7-Zip version:

1. Replace `7z/` directory with new source
2. Keep `7z-wrapper/` unchanged
3. Rebuild: `make -f makefile.gcc`

The wrapper uses stable 7-Zip APIs that rarely change.

## Supported Formats

7z, ZIP, RAR, TAR, GZIP, BZIP2, XZ, ISO, CAB, WIM, and many more.

## License

Same as 7-Zip (LGPL with unRAR restriction).
