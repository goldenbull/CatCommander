# 7z-Wrapper Migration Summary

## What Changed

All wrapper-specific files have been moved from `7z/` to a separate `7z-wrapper/` directory for easier 7-Zip source code upgrades.

## New Directory Structure

```
CatCommander/
├── 7z/                           # 7-Zip source code (unchanged)
│   └── CPP/7zip/...              # Can be upgraded independently
│
├── 7z-wrapper/                   # ← NEW: Wrapper code (isolated)
│   ├── include/
│   │   └── Sz7zCWrapper.h        # C API header
│   ├── src/
│   │   └── Sz7zCWrapper.cpp      # C++ implementation
│   ├── build/
│   │   ├── makefile              # Windows (nmake)
│   │   ├── makefile.gcc          # Linux/macOS (make)
│   │   └── Archive2Wrapper.def   # DLL exports
│   └── README.md                 # Documentation
│
└── CatCommander/SevenZip/        # C# wrapper classes
    ├── SevenZipNative.cs         # P/Invoke declarations
    └── SevenZipArchive.cs        # High-level API
```

## Building the Wrapper

The wrapper uses **build scripts** that generate temporary makefiles in the 7z source directory (where all the relative paths work correctly), then copy the output back to `7z-wrapper/build/`.

### Windows
```cmd
cd 7z-wrapper\build
build.cmd
```
Output: `7z-wrapper\build\7z.dll` and `7z.lib`

### Linux/macOS
```bash
cd 7z-wrapper/build
./build.sh
```
Output: `7z-wrapper/build/7z.so` or `7z.dylib`

### What the Build Scripts Do

1. Navigate to `7z/CPP/7zip/Bundles/Format7zF/`
2. Generate a temporary `makefile.wrapper` or `makefile.wrapper.gcc`
3. Set `WRAPPER_ROOT` variable pointing to `../../../../7z-wrapper`
4. Add wrapper include path: `-I$(WRAPPER_ROOT)/include`
5. Use wrapper DEF file: `DEF_FILE = $(WRAPPER_ROOT)/build/Archive2Wrapper.def`
6. Add wrapper object: `$O/Sz7zCWrapper.obj`
7. Compile and link everything
8. Copy result back to `7z-wrapper/build/`

This approach avoids path issues while keeping wrapper files separate from 7z source.

## Upgrading 7-Zip in the Future

```bash
# Step 1: Backup current version (optional)
mv 7z 7z-backup

# Step 2: Download and extract new 7-Zip source
# Download from https://www.7-zip.org/download.html
unzip 7z2409-src.zip -d 7z

# Step 3: Rebuild wrapper (no changes needed!)
cd 7z-wrapper/build
make -f makefile.gcc clean
make -f makefile.gcc

# Done! The wrapper now uses the new 7-Zip version
```

## Files Moved

### From 7z/ to 7z-wrapper/
- `7z/CPP/7zip/C/Sz7zCWrapper.h` → `7z-wrapper/include/Sz7zCWrapper.h`
- `7z/CPP/7zip/Archive/Sz7zCWrapper.cpp` → `7z-wrapper/src/Sz7zCWrapper.cpp`
- `7z/CPP/7zip/Archive/Archive2Wrapper.def` → `7z-wrapper/build/Archive2Wrapper.def`
- `7z/CPP/7zip/Bundles/Format7zF/makefile-wrapper` → `7z-wrapper/build/makefile`
- `7z/CPP/7zip/Bundles/Format7zF/makefile-wrapper.gcc` → `7z-wrapper/build/makefile.gcc`
- `7z/C_WRAPPER_API.md` → `7z-wrapper/README.md`

### Unchanged
- `CatCommander/SevenZip/SevenZipNative.cs` - Still references `7z` DLL
- `CatCommander/SevenZip/SevenZipArchive.cs` - No changes needed

## Benefits

✅ **Clean separation** - Wrapper code separate from 7-Zip source
✅ **Easy upgrades** - Replace `7z/` directory without touching wrapper
✅ **Version control friendly** - Can `.gitignore` the `7z/` directory if desired
✅ **Build simplicity** - Single build location in `7z-wrapper/build/`
✅ **No modifications to 7z source** - Original 7-Zip code remains pristine

## Testing the Build

After migrating, verify the build works:

```bash
cd 7z-wrapper/build
make -f makefile.gcc clean
make -f makefile.gcc

# Check exports
nm -g _o/7z.dylib | grep Sz7z_
```

You should see all the C wrapper functions exported:
- Sz7z_OpenArchive
- Sz7z_CloseArchive
- Sz7z_ExtractAll
- etc.

## Next Steps

1. Build the wrapper using the commands above
2. Copy the resulting DLL/dylib/so to your CatCommander output directory
3. Use the C# wrapper classes to work with archives

See `7z-wrapper/README.md` for complete API documentation and usage examples.
