namespace AllCodeChecker.Configuration;

public sealed record ConfigurationLoadResult(
    CheckerConfiguration Configuration,
    string? ConfigurationPath)
{
    public bool IsDefault => ConfigurationPath is null;
}
