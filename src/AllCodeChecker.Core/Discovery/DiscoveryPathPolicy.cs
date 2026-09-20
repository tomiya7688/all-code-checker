namespace AllCodeChecker.Discovery;

public sealed class DiscoveryPathPolicy
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".idea",
        ".vs",
        ".vscode",
        "bin",
        "dist",
        "generated",
        "node_modules",
        "obj",
        "vendor"
    };

    public bool IsExcludedDirectory(string directoryName) =>
        ExcludedDirectoryNames.Contains(directoryName);
}
