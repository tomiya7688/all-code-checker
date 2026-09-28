namespace AllCodeChecker.Discovery;

public sealed record DiscoveredSourceGroup(
    SupportedLanguage Language,
    string BoundaryPath,
    IReadOnlyList<string> SourceFiles);
