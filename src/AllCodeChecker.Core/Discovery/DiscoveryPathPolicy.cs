namespace AllCodeChecker.Discovery;

public sealed class DiscoveryPathPolicy
{
    private readonly HashSet<string> excludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
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
        excludedDirectoryNames.Contains(directoryName);
}
