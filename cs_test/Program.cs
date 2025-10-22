using SevenZip.net;

namespace cs_test;

class Program
{
    static void Main(string[] args)
    {
        var formats = NativeLib.GetAllFormatNames() + " xxxxx";
        foreach (var fmt in formats.Split(" "))
        {
            Console.WriteLine("==============");
            var info = new FormatInfo();
            var ret = NativeLib.GetFormatInfoByName(fmt, ref info);
            if (ret)
            {
                Console.WriteLine(info.Name);
                Console.WriteLine(info.Ext);
                Console.WriteLine(info.AddExt);
            }
            else
            {
                Console.WriteLine("unknown");
            }
        }

    }
}