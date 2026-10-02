using System.Text.Json;
using AllCodeChecker.Diagnostics;
using Xunit;

namespace AllCodeChecker.Cli.Tests;

public sealed class CliContractTests
{
    private static readonly string[] ExpectedDiagnosticOrder =
        ["ACI002", "ACI003", "ACI201"];

    [Fact]
    public void ParserUsesDocumentedDefaults()
    {
        CliOptions options = CliParser.Parse(["src"]);

        Assert.Equal("src", options.TargetPath);
        Assert.True(options.ShowDanger);
        Assert.True(options.ShowWarning);
        Assert.True(options.ShowNotice);
        Assert.False(options.CoverageRequested);
        Assert.Equal(CliFailureThreshold.Danger, options.FailureThreshold);
        Assert.Equal(CliOutputFormat.Text, options.OutputFormat);
    }

    [Fact]
    public void ParserAcceptsDocumentedOptions()
    {
        CliOptions options = CliParser.Parse(
        [
            "--config",
            "custom.json",
            "--no-warning",
            "--no-notice",
            "--coverage",
            "--fail-on",
            "notice",
            "--format",
            "sarif",
            "--output",
            "result.sarif",
            "src"
        ]);

        Assert.Equal("custom.json", options.ConfigurationPath);
        Assert.True(options.ShowDanger);
        Assert.False(options.ShowWarning);
        Assert.False(options.ShowNotice);
        Assert.True(options.CoverageRequested);
        Assert.Equal(CliFailureThreshold.Notice, options.FailureThreshold);
        Assert.Equal(CliOutputFormat.Sarif, options.OutputFormat);
        Assert.Equal("result.sarif", options.OutputPath);
    }

    [Fact]
    public void ParserRejectsUnknownOption()
    {
        CliUsageException exception = Assert.Throws<CliUsageException>(
            () => CliParser.Parse(["src", "--unknown"]));

        Assert.Contains("--unknown", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulAnalysisExitCodeHonorsThreshold()
    {
        CheckerDiagnostic[] diagnostics =
        [
            new("ACI201", DiagnosticSeverity.Notice, "notice"),
            new("ACI101", DiagnosticSeverity.Warning, "warning")
        ];

        Assert.Equal(
            CliExitCode.Success,
            CliExitCodeEvaluator.ForSuccessfulAnalysis(
                diagnostics,
                CliFailureThreshold.Danger));
        Assert.Equal(
            CliExitCode.DiagnosticsFound,
            CliExitCodeEvaluator.ForSuccessfulAnalysis(
                diagnostics,
                CliFailureThreshold.Warning));
        Assert.Equal(
            CliExitCode.DiagnosticsFound,
            CliExitCodeEvaluator.ForSuccessfulAnalysis(
                diagnostics,
                CliFailureThreshold.Notice));
        Assert.Equal(
            CliExitCode.Success,
            CliExitCodeEvaluator.ForSuccessfulAnalysis(
                diagnostics,
                CliFailureThreshold.Never));
    }

    [Fact]
    public void DiagnosticOrderingIsStable()
    {
        CheckerDiagnostic[] diagnostics =
        [
            new("ACI201", DiagnosticSeverity.Notice, "third", "b.cs", 1, 1),
            new("ACI003", DiagnosticSeverity.Danger, "second", "a.cs", 2, 1),
            new("ACI002", DiagnosticSeverity.Danger, "first", "a.cs", 1, 1)
        ];

        IReadOnlyList<CheckerDiagnostic> ordered =
            CliDiagnosticOrdering.Stable(diagnostics);

        Assert.Equal(
            ExpectedDiagnosticOrder,
            ordered.Select(diagnostic => diagnostic.RuleId));
    }

    [Fact]
    public void TextOutputUsesJapaneseSeverityAndLocation()
    {
        CheckerDiagnostic[] diagnostics =
        [
            new(
                "ACI003",
                DiagnosticSeverity.Danger,
                "未解決シンボルです。",
                "src/main.cs",
                12,
                8,
                "csharp")
        ];

        string output = CliOutputRenderer.Render(
            CliOutputFormat.Text,
            "/repo",
            null,
            diagnostics);

        Assert.Contains(
            "ACI003 危険 未解決シンボルです。 [src/main.cs:12:8]",
            output,
            StringComparison.Ordinal);
        Assert.Contains("全体結果: 危険", output, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonOutputIsMachineReadable()
    {
        CheckerDiagnostic[] diagnostics =
        [
            new("ACI201", DiagnosticSeverity.Notice, "確認してください。")
        ];

        string output = CliOutputRenderer.Render(
            CliOutputFormat.Json,
            "/repo",
            "/repo/all-code-checker.json",
            diagnostics);

        using JsonDocument document = JsonDocument.Parse(output);

        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(
            "注意",
            document.RootElement
                .GetProperty("diagnostics")[0]
                .GetProperty("severity")
                .GetString());
    }

    [Fact]
    public void SarifOutputUsesSarifVersionAndRuleId()
    {
        CheckerDiagnostic[] diagnostics =
        [
            new("ACI101", DiagnosticSeverity.Warning, "確認してください。")
        ];

        string output = CliOutputRenderer.Render(
            CliOutputFormat.Sarif,
            "/repo",
            null,
            diagnostics);

        using JsonDocument document = JsonDocument.Parse(output);

        Assert.Equal("2.1.0", document.RootElement.GetProperty("version").GetString());
        Assert.Equal(
            "ACI101",
            document.RootElement
                .GetProperty("runs")[0]
                .GetProperty("results")[0]
                .GetProperty("ruleId")
                .GetString());
    }

    [Fact]
    public void ApplicationReturnsInputErrorForUnsupportedSingleFile()
    {
        using var fixture = new CliFixture();
        string target = fixture.CreateFile("main.rb", "puts 'hello'");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = CliApplication.Run([target], output, error);

        Assert.Equal((int)CliExitCode.InputError, exitCode);
        Assert.Contains("未対応", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationSmokeTestAcceptsSupportedDirectory()
    {
        using var fixture = new CliFixture();
        fixture.CreateFile("src/main.cs", "internal sealed class MainType {}");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = CliApplication.Run(
            [fixture.Root, "--fail-on", "notice"],
            output,
            error);

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Contains("全体結果: 正常", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void ApplicationWritesRequestedOutputFile()
    {
        using var fixture = new CliFixture();
        fixture.CreateFile("src/main.py", "value = 1");
        string resultPath = Path.Combine(fixture.Root, "artifacts", "result.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = CliApplication.Run(
            [
                fixture.Root,
                "--format",
                "json",
                "--output",
                resultPath
            ],
            output,
            error);

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.True(File.Exists(resultPath));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(resultPath));
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
    }

    private sealed class CliFixture : IDisposable
    {
        public CliFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                $"all-code-checker-cli-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateFile(string relativePath, string content)
        {
            string path = Path.GetFullPath(Path.Combine(Root, relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
