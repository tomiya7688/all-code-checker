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
        if (!OperatingSystem.IsWindows() ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FASTPARSE_LIBRARY_PATH")))
        {
            return;
        }

        var nativePath = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "fastparse.dll");
        if (File.Exists(nativePath))
        {
            Environment.SetEnvironmentVariable("FASTPARSE_LIBRARY_PATH", nativePath);
        }
    }
}
