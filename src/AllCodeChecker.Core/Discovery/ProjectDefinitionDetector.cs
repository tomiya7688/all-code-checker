namespace AllCodeChecker.Discovery;

public static class ProjectDefinitionDetector
{
    public static bool IsProjectDefinition(string path)
    {
        string fileName = Path.GetFileName(path);

        if (fileName.Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("go.mod", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string extension = Path.GetExtension(path);

        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    public static bool SupportsLanguage(string path, SupportedLanguage language)
    {
        string fileName = Path.GetFileName(path);
        string extension = Path.GetExtension(path);

        return language switch
        {
            SupportedLanguage.CSharp =>
                extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase),
            SupportedLanguage.TypeScript =>
                fileName.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase),
            SupportedLanguage.Python =>
                fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase),
            SupportedLanguage.Go =>
                fileName.Equals("go.mod", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
