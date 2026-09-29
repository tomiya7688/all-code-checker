using System.Text.Json;
using System.Text.Json.Serialization;
using AllCodeChecker.Diagnostics;
using AllCodeChecker.Results;

namespace AllCodeChecker.Cli;

internal static class CliOutputRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Render(
        CliOutputFormat format,
        string targetPath,
        string? configurationPath,
        IReadOnlyList<CheckerDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return format switch
        {
            CliOutputFormat.Text => RenderText(targetPath, configurationPath, diagnostics),
            CliOutputFormat.Json => RenderJson(targetPath, configurationPath, diagnostics),
            CliOutputFormat.Sarif => RenderSarif(diagnostics),
            CliOutputFormat.GitHub => RenderGitHub(diagnostics),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    private static string RenderText(
        string targetPath,
        string? configurationPath,
        IReadOnlyList<CheckerDiagnostic> diagnostics)
    {
        var lines = new List<string>
        {
            $"解析対象: {targetPath}",
            configurationPath is null
                ? "設定: 既定"
                : $"設定: {configurationPath}"
        };

        lines.AddRange(diagnostics.Select(RenderDiagnosticLine));
        lines.Add($"全体結果: {OverallLabel(diagnostics)}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderJson(
        string targetPath,
        string? configurationPath,
        IReadOnlyList<CheckerDiagnostic> diagnostics)
    {
        var document = new
        {
            version = 1,
            target = targetPath,
            configuration = configurationPath,
            diagnostics = diagnostics.Select(diagnostic => new
            {
                rule = diagnostic.RuleId,
                severity = SeverityLabel(diagnostic.Severity),
                reason = diagnostic.Reason,
                language = diagnostic.Language,
                path = diagnostic.Path,
                line = diagnostic.Line,
                column = diagnostic.Column
            }),
            overall = OverallLabel(diagnostics)
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static string RenderSarif(IReadOnlyList<CheckerDiagnostic> diagnostics)
    {
        object[] results = diagnostics
            .Select(CreateSarifResult)
            .ToArray();

        var document = new
        {
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            version = "2.1.0",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "all-code-checker"
                        }
                    },
                    results
                }
            }
        };

        string json = JsonSerializer.Serialize(document, JsonOptions);
        return json.Replace(""schema":", ""$schema":", StringComparison.Ordinal);
    }

    private static object CreateSarifResult(CheckerDiagnostic diagnostic)
    {
        object[]? locations = diagnostic.Path is null
            ? null
            :
            [
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new
                        {
                            uri = diagnostic.Path.Replace('\\', '/')
                        },
                        region = diagnostic.Line is null
                            ? null
                            : new
                            {
                                startLine = diagnostic.Line,
                                startColumn = diagnostic.Column
                            }
                    }
                }
            ];

        return new
        {
            ruleId = diagnostic.RuleId,
            level = diagnostic.Severity switch
            {
                DiagnosticSeverity.Danger => "error",
                DiagnosticSeverity.Warning => "warning",
                DiagnosticSeverity.Notice => "note",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(diagnostic),
                    diagnostic.Severity,
                    null)
            },
            message = new
            {
                text = diagnostic.Reason
            },
            locations
        };
    }

    private static string RenderGitHub(IReadOnlyList<CheckerDiagnostic> diagnostics)
    {
        var lines = diagnostics.Select(diagnostic =>
        {
            string level = diagnostic.Severity switch
            {
                DiagnosticSeverity.Danger => "error",
                DiagnosticSeverity.Warning => "warning",
                DiagnosticSeverity.Notice => "notice",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(diagnostic),
                    diagnostic.Severity,
                    null)
            };

            var properties = new List<string>();

            if (diagnostic.Path is not null)
            {
                properties.Add($"file={EscapeProperty(diagnostic.Path)}");
            }

            if (diagnostic.Line is not null)
            {
                properties.Add($"line={diagnostic.Line.Value}");
            }

            if (diagnostic.Column is not null)
            {
                properties.Add($"col={diagnostic.Column.Value}");
            }

            properties.Add($"title={EscapeProperty(diagnostic.RuleId)}");

            return $"::{level} {string.Join(',', properties)}::{EscapeMessage(diagnostic.Reason)}";
        });

        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderDiagnosticLine(CheckerDiagnostic diagnostic)
    {
        string location = diagnostic.Path is null
            ? string.Empty
            : $" [{LocationText(diagnostic)}]";

        return $"{diagnostic.RuleId} {SeverityLabel(diagnostic.Severity)} {diagnostic.Reason}{location}";
    }

    private static string LocationText(CheckerDiagnostic diagnostic)
    {
        if (diagnostic.Line is null)
        {
            return diagnostic.Path!;
        }

        if (diagnostic.Column is null)
        {
            return $"{diagnostic.Path}:{diagnostic.Line.Value}";
        }

        return $"{diagnostic.Path}:{diagnostic.Line.Value}:{diagnostic.Column.Value}";
    }

    private static string OverallLabel(IReadOnlyList<CheckerDiagnostic> diagnostics)
    {
        DiagnosticSeverity? severity = OverallStatus.Calculate(diagnostics);

        return severity is null
            ? "正常"
            : SeverityLabel(severity.Value);
    }

    private static string SeverityLabel(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Danger => "危険",
            DiagnosticSeverity.Warning => "警告",
            DiagnosticSeverity.Notice => "注意",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };

    private static string EscapeMessage(string value) =>
        value.Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string EscapeProperty(string value) =>
        EscapeMessage(value)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);
}
