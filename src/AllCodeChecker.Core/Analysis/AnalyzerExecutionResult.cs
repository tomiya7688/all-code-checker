using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Analysis;

public sealed record AnalyzerExecutionResult
{
    private AnalyzerExecutionResult(
        AnalyzerCompletionStatus status,
        IReadOnlyList<CheckerDiagnostic> diagnostics,
        AnalyzerFailureKind? failureKind,
        string? failureReason,
        IReadOnlyList<AnalyzerAnalysisData> analysisData)
    {
        Status = status;
        Diagnostics = diagnostics;
        FailureKind = failureKind;
        FailureReason = failureReason;
        AnalysisData = analysisData;
    }

    public AnalyzerCompletionStatus Status { get; }
    public IReadOnlyList<CheckerDiagnostic> Diagnostics { get; }
    public AnalyzerFailureKind? FailureKind { get; }
    public string? FailureReason { get; }
    public IReadOnlyList<AnalyzerAnalysisData> AnalysisData { get; }

    public static AnalyzerExecutionResult Complete(
        IEnumerable<CheckerDiagnostic>? diagnostics = null,
        IEnumerable<AnalyzerAnalysisData>? analysisData = null) =>
        new(AnalyzerCompletionStatus.Complete, MaterializeDiagnostics(diagnostics), null, null,
            MaterializeAnalysisData(analysisData));

    public static AnalyzerExecutionResult Partial(
        string reason,
        IEnumerable<CheckerDiagnostic>? diagnostics = null,
        IEnumerable<AnalyzerAnalysisData>? analysisData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(AnalyzerCompletionStatus.Partial, MaterializeDiagnostics(diagnostics), null, reason,
            MaterializeAnalysisData(analysisData));
    }

    public static AnalyzerExecutionResult Failed(
        AnalyzerFailureKind failureKind,
        string reason,
        IEnumerable<CheckerDiagnostic>? diagnostics = null,
        IEnumerable<AnalyzerAnalysisData>? analysisData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(AnalyzerCompletionStatus.Failed, MaterializeDiagnostics(diagnostics), failureKind, reason,
            MaterializeAnalysisData(analysisData));
    }

    private static CheckerDiagnostic[] MaterializeDiagnostics(IEnumerable<CheckerDiagnostic>? diagnostics) =>
        diagnostics?.ToArray() ?? [];

    private static AnalyzerAnalysisData[] MaterializeAnalysisData(IEnumerable<AnalyzerAnalysisData>? analysisData) =>
        analysisData?.ToArray() ?? [];
}
