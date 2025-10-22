using System.Runtime.InteropServices;

namespace SevenZip.net;

public partial class NativeLib
{
    [LibraryImport("lib/7z")]
    public static partial int my_wrapper_func(int n);

    void ListAllFormats()
    {
        /*
             UINT32 numFormats = 0;
           GetNumberOfFormats(&numFormats);
           for (int i = 0; i < numFormats; ++i)
           {
               auto info = g_Arcs[i];
               std::cout << std::format("[{:2}] {}", i, info->Name);
               std::cout << std::format(" Extensions: {}\n", info->Ext);
               std::cout << std::format("    AddExt:    {}\n", info->AddExt);
               std::cout << std::format("    Flags:     0x{:08X}\n", info->Flags);
               std::cout << std::format("    TimeFlags: 0x{:08X}\n", info->TimeFlags);
           }

         */
    }
}