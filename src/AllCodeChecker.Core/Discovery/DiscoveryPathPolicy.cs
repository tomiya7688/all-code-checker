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

    private readonly PathIgnoreMatcher ignoreMatcher;

    public DiscoveryPathPolicy(IEnumerable<string>? ignoredPaths = null)
    {
        ignoreMatcher = new PathIgnoreMatcher(ignoredPaths);
    }

    public bool IsExcludedDirectory(string root, string directoryPath)
    {
        string directoryName = Path.GetFileName(directoryPath);

        return excludedDirectoryNames.Contains(directoryName)
            || ignoreMatcher.IsMatch(Path.GetRelativePath(root, directoryPath));
    }

    public bool IsExcludedFile(string root, string filePath) =>
        ignoreMatcher.IsMatch(Path.GetRelativePath(root, filePath));
}
