using AllCodeChecker.Discovery;

namespace AllCodeChecker.Analysis;

public sealed record AnalyzerRequest(
    DiscoveredSourceGroup SourceGroup,
    AnalysisInputKind InputKind = AnalysisInputKind.Directory,
    string? InputPath = null,
    string? EntryFile = null);
