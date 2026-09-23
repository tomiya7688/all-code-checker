using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Results;

public static class OverallStatus
{
    public static DiagnosticSeverity? Calculate(
        IEnumerable<CheckerDiagnostic> diagnostics,
        SeveritySelection? enabledSeverities = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        SeveritySelection selection = enabledSeverities ?? SeveritySelection.All;
        DiagnosticSeverity? strongest = null;

        foreach (CheckerDiagnostic diagnostic in diagnostics)
        {
            if (!selection.IsEnabled(diagnostic.Severity))
            {
                continue;
            }

            if (strongest is null || diagnostic.Severity > strongest)
            {
                strongest = diagnostic.Severity;
            }
        }

        return strongest;
    }
}
