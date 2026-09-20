namespace AllCodeChecker.Diagnostics;

public sealed record CheckerDiagnostic(
    string RuleId,
    DiagnosticSeverity Severity,
    string Reason,
    string? Path = null,
    int? Line = null,
    int? Column = null);
