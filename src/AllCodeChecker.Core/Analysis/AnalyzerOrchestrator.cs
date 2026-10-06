using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;

namespace AllCodeChecker.Analysis;

public sealed class AnalyzerOrchestrator
{
    private readonly Dictionary<SupportedLanguage, ILanguageAnalyzer> analyzers;

    public AnalyzerOrchestrator(IEnumerable<ILanguageAnalyzer> analyzers)
    {
        ArgumentNullException.ThrowIfNull(analyzers);

        var registered = new Dictionary<SupportedLanguage, ILanguageAnalyzer>();

        foreach (ILanguageAnalyzer analyzer in analyzers)
        {
            ArgumentNullException.ThrowIfNull(analyzer);

            if (!registered.TryAdd(analyzer.Language, analyzer))
            {
                throw new ArgumentException(
                    $"{analyzer.Language} Analyzerが重複登録されています。",
                    nameof(analyzers));
            }
        }

        this.analyzers = registered;
    }

    public async Task<AnalysisOrchestrationResult> AnalyzeAsync(
        AnalysisDiscoveryResult discovery,
        AnalyzerExecutionMode executionMode = AnalyzerExecutionMode.Parallel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        cancellationToken.ThrowIfCancellationRequested();

        AnalyzerScopeResult[] scopes = executionMode switch
        {
            AnalyzerExecutionMode.Parallel =>
                await AnalyzeParallelAsync(discovery.Groups, cancellationToken)
                    .ConfigureAwait(false),
            AnalyzerExecutionMode.Sequential =>
                await AnalyzeSequentialAsync(discovery.Groups, cancellationToken)
                    .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(
                nameof(executionMode),
                executionMode,
                null)
        };

        IReadOnlyList<CheckerDiagnostic> diagnostics =
            DiagnosticDeduplicator.Deduplicate(
                scopes.SelectMany(scope => scope.Diagnostics));

        return new AnalysisOrchestrationResult(scopes, diagnostics);
    }

    private async Task<AnalyzerScopeResult[]> AnalyzeParallelAsync(
        IReadOnlyList<DiscoveredSourceGroup> groups,
        CancellationToken cancellationToken)
    {
        Task<AnalyzerScopeResult>[] tasks = groups
            .Select(group => AnalyzeScopeAsync(group, cancellationToken))
            .ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<AnalyzerScopeResult[]> AnalyzeSequentialAsync(
        IReadOnlyList<DiscoveredSourceGroup> groups,
        CancellationToken cancellationToken)
    {
        var results = new List<AnalyzerScopeResult>(groups.Count);

        foreach (DiscoveredSourceGroup group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(
                await AnalyzeScopeAsync(group, cancellationToken)
                    .ConfigureAwait(false));
        }

        return results.ToArray();
    }

    private async Task<AnalyzerScopeResult> AnalyzeScopeAsync(
        DiscoveredSourceGroup group,
        CancellationToken cancellationToken)
    {
        if (!analyzers.TryGetValue(group.Language, out ILanguageAnalyzer? analyzer))
        {
            CheckerDiagnostic missingAnalyzer =
                AnalyzerFailureDiagnosticFactory.MissingAnalyzer(group);

            return new AnalyzerScopeResult(
                group.Language,
                group.BoundaryPath,
                AnalyzerCompletionStatus.Partial,
                [missingAnalyzer]);
        }

        try
        {
            AnalyzerExecutionResult result = await analyzer
                .AnalyzeAsync(new AnalyzerRequest(group), cancellationToken)
                .ConfigureAwait(false);

            if (result is null)
            {
                throw new InvalidOperationException(
                    "Analyzerがnull resultを返しました。");
            }

            return BuildScopeResult(group, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            CheckerDiagnostic internalError =
                AnalyzerFailureDiagnosticFactory.FromUnhandledException(
                    group,
                    exception);

            return new AnalyzerScopeResult(
                group.Language,
                group.BoundaryPath,
                AnalyzerCompletionStatus.Failed,
                [internalError]);
        }
    }

    private static AnalyzerScopeResult BuildScopeResult(
        DiscoveredSourceGroup group,
        AnalyzerExecutionResult executionResult)
    {
        var diagnostics = new List<CheckerDiagnostic>(
            executionResult.Diagnostics.Count + 1);
        diagnostics.AddRange(executionResult.Diagnostics);

        if (executionResult.Status != AnalyzerCompletionStatus.Complete)
        {
            diagnostics.Add(
                AnalyzerFailureDiagnosticFactory.FromExecutionResult(
                    group,
                    executionResult));
        }

        return new AnalyzerScopeResult(
            group.Language,
            group.BoundaryPath,
            executionResult.Status,
            DiagnosticDeduplicator.Deduplicate(diagnostics));
    }
}
