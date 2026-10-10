using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Results;

public static class DiagnosticFilter
{
    public static IReadOnlyList<CheckerDiagnostic> Apply(
        IEnumerable<CheckerDiagnostic> diagnostics,
        SeveritySelection enabledSeverities)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics
            .Where(diagnostic => enabledSeverities.IsEnabled(diagnostic.Severity))
            .ToArray();
    }
}
