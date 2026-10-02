using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Cli;

internal static class CliExitCodeEvaluator
{
    public static CliExitCode ForSuccessfulAnalysis(
        IEnumerable<CheckerDiagnostic> diagnostics,
        CliFailureThreshold threshold)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (threshold == CliFailureThreshold.Never)
        {
            return CliExitCode.Success;
        }

        bool shouldFail = diagnostics.Any(diagnostic =>
            MeetsThreshold(diagnostic.Severity, threshold));

        return shouldFail
            ? CliExitCode.DiagnosticsFound
            : CliExitCode.Success;
    }

    private static bool MeetsThreshold(
        DiagnosticSeverity severity,
        CliFailureThreshold threshold) =>
        threshold switch
        {
            CliFailureThreshold.Danger => severity == DiagnosticSeverity.Danger,
            CliFailureThreshold.Warning => severity is DiagnosticSeverity.Danger or DiagnosticSeverity.Warning,
            CliFailureThreshold.Notice => true,
            CliFailureThreshold.Never => false,
            _ => throw new ArgumentOutOfRangeException(nameof(threshold), threshold, null)
        };
}
