using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;

namespace AllCodeChecker.Analysis;

public sealed record AnalyzerScopeResult(
    SupportedLanguage Language,
    string BoundaryPath,
    AnalyzerCompletionStatus Status,
    IReadOnlyList<CheckerDiagnostic> Diagnostics);
