using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Analysis;

public sealed record AnalyzerExecutionResult
{
    private AnalyzerExecutionResult(
        AnalyzerCompletionStatus status,
        IReadOnlyList<CheckerDiagnostic> diagnostics,
        AnalyzerFailureKind? failureKind,
        string? failureReason)
    {
        Status = status;
        Diagnostics = diagnostics;
        FailureKind = failureKind;
        FailureReason = failureReason;
    }

    public AnalyzerCompletionStatus Status { get; }

    public IReadOnlyList<CheckerDiagnostic> Diagnostics { get; }

    public AnalyzerFailureKind? FailureKind { get; }

    public string? FailureReason { get; }

    public static AnalyzerExecutionResult Complete(
        IEnumerable<CheckerDiagnostic>? diagnostics = null) =>
        new(
            AnalyzerCompletionStatus.Complete,
            MaterializeDiagnostics(diagnostics),
            null,
            null);

    public static AnalyzerExecutionResult Partial(
        string reason,
        IEnumerable<CheckerDiagnostic>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new(
            AnalyzerCompletionStatus.Partial,
            MaterializeDiagnostics(diagnostics),
            null,
            reason);
    }

    public static AnalyzerExecutionResult Failed(
        AnalyzerFailureKind failureKind,
        string reason,
        IEnumerable<CheckerDiagnostic>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new(
            AnalyzerCompletionStatus.Failed,
            MaterializeDiagnostics(diagnostics),
            failureKind,
            reason);
    }

    private static CheckerDiagnostic[] MaterializeDiagnostics(
        IEnumerable<CheckerDiagnostic>? diagnostics) =>
        diagnostics?.ToArray() ?? [];
}
