namespace AllCodeChecker.Discovery;

public sealed record AnalysisDiscoveryResult(
    string InputPath,
    AnalysisInputKind InputKind,
    IReadOnlyList<DiscoveredSourceGroup> Groups,
    IReadOnlyList<string> UnsupportedFiles,
    string? EntryFile = null);
