using System.Reflection;
using System.Runtime.InteropServices;

namespace AllCodeChecker.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        ConfigurePublishedFastParseNativeLibrary();
        return CliApplication.Run(args, Console.Out, Console.Error);
    }

    private static void ConfigurePublishedFastParseNativeLibrary()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var nativePath = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "fastparse.dll");
        if (!File.Exists(nativePath))
        {
            return;
        }

        var fastParseAssembly = Assembly.Load("FastParse");
        NativeLibrary.SetDllImportResolver(fastParseAssembly, (libraryName, _, _) =>
            string.Equals(Path.GetFileNameWithoutExtension(libraryName), "fastparse", StringComparison.OrdinalIgnoreCase)
                ? NativeLibrary.Load(nativePath)
                : IntPtr.Zero);
    }
}
