using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SevenZip.net;

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public struct FormatInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Ext;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string AddExt;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    byte[] ClassID_data;

    public Guid ClassID => new Guid(ClassID_data);
}

public partial class NativeLib
{
    [LibraryImport("lib/7z", EntryPoint = "GetAllFormatNames")]
    private static partial IntPtr _GetAllFormatNames();

    public static unsafe string GetAllFormatNames()
    {
        var s = _GetAllFormatNames();
        return Marshal.PtrToStringUni(s) ?? "";
    }

    [DllImport("lib/7z", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFormatInfoByName(string name, ref FormatInfo info);


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