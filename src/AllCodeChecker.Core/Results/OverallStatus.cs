using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Results;

public static class OverallStatus
{
    public static DiagnosticSeverity? Calculate(IEnumerable<CheckerDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        DiagnosticSeverity? strongest = null;

        foreach (CheckerDiagnostic diagnostic in diagnostics)
        {
            if (strongest is null || diagnostic.Severity > strongest)
            {
                strongest = diagnostic.Severity;
            }
        }

        return strongest;
    }
}
