using System.Runtime.InteropServices;

namespace SevenZip.net;

public class NativeLib
{
    
    [DllImport("lib/7z")]
    public static extern int my_wrapper_func(int n);

}