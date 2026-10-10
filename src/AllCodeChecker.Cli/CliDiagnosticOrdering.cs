using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Cli;

internal static class CliDiagnosticOrdering
{
    public static IReadOnlyList<CheckerDiagnostic> Stable(
        IEnumerable<CheckerDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics
            .OrderBy(diagnostic => diagnostic.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Line ?? 0)
            .ThenBy(diagnostic => diagnostic.Column ?? 0)
            .ThenBy(diagnostic => diagnostic.RuleId, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Reason, StringComparer.Ordinal)
            .ToArray();
    }
}
