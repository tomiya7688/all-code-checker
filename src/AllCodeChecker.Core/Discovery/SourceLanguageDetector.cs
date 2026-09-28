namespace AllCodeChecker.Discovery;

public static class SourceLanguageDetector
{
    public static bool TryDetect(string path, out SupportedLanguage language)
    {
        string extension = Path.GetExtension(path);

        language = extension.ToLowerInvariant() switch
        {
            ".cs" => SupportedLanguage.CSharp,
            ".ts" or ".tsx" => SupportedLanguage.TypeScript,
            ".py" => SupportedLanguage.Python,
            ".go" => SupportedLanguage.Go,
            _ => default
        };

        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".py", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".go", StringComparison.OrdinalIgnoreCase);
    }
}
