using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Analysis;

public static class DiagnosticDeduplicator
{
    public static IReadOnlyList<CheckerDiagnostic> Deduplicate(
        IEnumerable<CheckerDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics
            .Distinct()
            .ToArray();
    }
}
