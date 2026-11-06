using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Analysis;

public sealed record AnalysisOrchestrationResult(
    IReadOnlyList<AnalyzerScopeResult> Scopes,
    IReadOnlyList<CheckerDiagnostic> Diagnostics)
{
    public bool IsComplete =>
        Scopes.All(scope => scope.Status == AnalyzerCompletionStatus.Complete);
}
