using AllCodeChecker.Diagnostics;

namespace AllCodeChecker.Results;

public readonly record struct SeveritySelection(
    bool Danger,
    bool Warning,
    bool Notice)
{
    public static SeveritySelection All { get; } = new(
        Danger: true,
        Warning: true,
        Notice: true);

    public bool IsEnabled(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Danger => Danger,
            DiagnosticSeverity.Warning => Warning,
            DiagnosticSeverity.Notice => Notice,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "未定義のSeverityです。")
        };
}
