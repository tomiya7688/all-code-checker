using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;

namespace AllCodeChecker.Analysis;

public sealed class TypeScriptProjectAnalyzer : ILanguageAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SupportedLanguage Language => SupportedLanguage.TypeScript;

    public async Task<AnalyzerExecutionResult> AnalyzeAsync(
        AnalyzerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string runtimeDirectory = Path.Combine(AppContext.BaseDirectory, "typescript-runtime");
        string executable = Path.Combine(runtimeDirectory,
            OperatingSystem.IsWindows() ? "node.exe" : "node");
        string helper = Path.Combine(runtimeDirectory, "analyze.cjs");
        if (!File.Exists(executable) || !File.Exists(helper)
            || !File.Exists(Path.Combine(runtimeDirectory, "node_modules", "typescript", "package.json")))
        {
            return AnalyzerExecutionResult.Failed(AnalyzerFailureKind.ProcessFailure,
                "同梱TypeScript helper、Compiler API、またはNode runtimeが見つかりません。");
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            WorkingDirectory = runtimeDirectory
        };
        startInfo.ArgumentList.Add(helper);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            bool started = false;
            try
            {
                started = process.Start();
                if (!started)
                {
                    return AnalyzerExecutionResult.Failed(AnalyzerFailureKind.ProcessFailure,
                        "TypeScript helperを起動できませんでした。");
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                string payload = JsonSerializer.Serialize(new HelperRequest(
                    request.SourceGroup.BoundaryPath,
                    request.SourceGroup.SourceFiles,
                    request.InputKind.ToString()), JsonOptions);
                Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token).ConfigureAwait(false);
                process.StandardInput.Close();
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                string stdout = await output.ConfigureAwait(false);
                string stderr = await error.ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    return AnalyzerExecutionResult.Failed(AnalyzerFailureKind.ProcessFailure,
                        $"TypeScript helperが終了コード{process.ExitCode}で失敗しました: {Trim(stderr)}");
                }

                HelperResponse? response = JsonSerializer.Deserialize<HelperResponse>(stdout, JsonOptions);
                if (response is null || response.Diagnostics is null || response.Symbols is null
                    || response.TestScripts is null || string.IsNullOrWhiteSpace(response.ProjectPath))
                {
                    return AnalyzerExecutionResult.Failed(AnalyzerFailureKind.ProcessFailure,
                        "TypeScript helperの応答形式が不正です。");
                }

                CheckerDiagnostic[] diagnostics = response.Diagnostics.Select(item => new CheckerDiagnostic(
                    item.RuleId,
                    Enum.Parse<DiagnosticSeverity>(item.Severity),
                    item.Reason,
                    item.Path is null ? null : Path.GetFullPath(item.Path),
                    item.Line,
                    item.Column,
                    "typescript")).ToArray();
                var data = new TypeScriptProjectAnalysisData(
                    response.ProjectPath,
                    response.StrictNullChecks,
                    response.Symbols.Select(item => new TypeScriptSymbolSummary(
                        Path.GetFullPath(item.FilePath), item.Name, item.Type, item.Line,
                        item.IsAny, item.IsUnknown, item.IncludesNull,
                        item.IncludesUndefined, item.Properties)).ToArray(),
                    response.TestScripts);

                return response.Partial
                    ? AnalyzerExecutionResult.Partial(
                        response.Reason ?? "TypeScript projectの一部を解析できませんでした。",
                        diagnostics, [data])
                    : AnalyzerExecutionResult.Complete(diagnostics, [data]);
            }
            finally
            {
                if (started && !process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        // The process exited between HasExited and Kill.
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return AnalyzerExecutionResult.Failed(AnalyzerFailureKind.ProcessFailure,
                $"TypeScript helperとの通信に失敗しました: {exception.Message}");
        }
    }

    private static string Trim(string message) =>
        message.Length <= 500 ? message : message[..500];

    private sealed record HelperRequest(
        string BoundaryPath,
        IReadOnlyList<string> SourceFiles,
        string InputKind);

    private sealed record HelperResponse(
        bool Partial,
        string? Reason,
        string ProjectPath,
        bool StrictNullChecks,
        IReadOnlyList<HelperDiagnostic> Diagnostics,
        IReadOnlyList<HelperSymbol> Symbols,
        IReadOnlyList<string> TestScripts);

    private sealed record HelperDiagnostic(
        string RuleId,
        string Severity,
        string Reason,
        string? Path,
        int? Line,
        int? Column);

    private sealed record HelperSymbol(
        string FilePath,
        string Name,
        string Type,
        int Line,
        bool IsAny,
        bool IsUnknown,
        bool IncludesNull,
        bool IncludesUndefined,
        IReadOnlyList<string> Properties);
}
