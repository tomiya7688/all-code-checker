using AllCodeChecker.Analysis;
using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;
using Xunit;

namespace AllCodeChecker.Core.Tests;

public sealed class AnalyzerOrchestratorTests
{
    [Fact]
    public async Task ParallelRunMergesDiagnosticsFromMultipleLanguages()
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.CSharp, "/repo/csharp", "/repo/csharp/App.cs"),
            Group(SupportedLanguage.Python, "/repo/python", "/repo/python/app.py"));

        ILanguageAnalyzer[] analyzers =
        [
            FakeAnalyzer.Complete(
                SupportedLanguage.CSharp,
                new CheckerDiagnostic(
                    "ACI003",
                    DiagnosticSeverity.Danger,
                    "C# diagnostic",
                    "/repo/csharp/App.cs",
                    Language: "csharp")),
            FakeAnalyzer.Complete(
                SupportedLanguage.Python,
                new CheckerDiagnostic(
                    "ACI201",
                    DiagnosticSeverity.Notice,
                    "Python diagnostic",
                    "/repo/python/app.py",
                    Language: "python"))
        ];

        var orchestrator = new AnalyzerOrchestrator(analyzers);

        AnalysisOrchestrationResult result = await orchestrator.AnalyzeAsync(
            discovery,
            AnalyzerExecutionMode.Parallel);

        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Scopes.Count);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI201");
    }

    [Fact]
    public async Task DuplicateDiagnosticsAreCollapsedAcrossScopes()
    {
        var duplicate = new CheckerDiagnostic(
            "ACI201",
            DiagnosticSeverity.Notice,
            "duplicate",
            "/repo/shared.ts",
            10,
            2,
            "typescript");

        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.TypeScript, "/repo/a", "/repo/a/a.ts"),
            Group(SupportedLanguage.TypeScript, "/repo/b", "/repo/b/b.ts"));

        var analyzer = new FakeAnalyzer(
            SupportedLanguage.TypeScript,
            (_, _) => Task.FromResult(
                AnalyzerExecutionResult.Complete([duplicate with { }])));

        var orchestrator = new AnalyzerOrchestrator([analyzer]);

        AnalysisOrchestrationResult result =
            await orchestrator.AnalyzeAsync(discovery);

        Assert.Single(result.Diagnostics);
        Assert.Equal(duplicate, result.Diagnostics[0]);
    }

    [Fact]
    public async Task MissingAnalyzerBecomesPartialAnalysisDanger()
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.Go, "/repo/go", "/repo/go/main.go"));

        var orchestrator = new AnalyzerOrchestrator([]);

        AnalysisOrchestrationResult result =
            await orchestrator.AnalyzeAsync(discovery);

        Assert.False(result.IsComplete);
        AnalyzerScopeResult scope = Assert.Single(result.Scopes);
        Assert.Equal(AnalyzerCompletionStatus.Partial, scope.Status);

        CheckerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ACI903", diagnostic.RuleId);
        Assert.Equal(DiagnosticSeverity.Danger, diagnostic.Severity);
        Assert.Equal("go", diagnostic.Language);
    }

    [Fact]
    public async Task UnhandledAnalyzerExceptionBecomesAci901WithoutHidingOtherResults()
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.CSharp, "/repo/csharp", "/repo/csharp/App.cs"),
            Group(SupportedLanguage.Python, "/repo/python", "/repo/python/app.py"));

        var crashing = new FakeAnalyzer(
            SupportedLanguage.CSharp,
            (_, _) => throw new InvalidOperationException("boom"));
        ILanguageAnalyzer healthy = FakeAnalyzer.Complete(
            SupportedLanguage.Python,
            new CheckerDiagnostic(
                "ACI205",
                DiagnosticSeverity.Notice,
                "healthy result",
                "/repo/python/app.py",
                Language: "python"));

        var orchestrator = new AnalyzerOrchestrator([crashing, healthy]);

        AnalysisOrchestrationResult result =
            await orchestrator.AnalyzeAsync(discovery);

        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI901");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI205");
    }

    [Fact]
    public async Task AnalyzerProcessFailureBecomesAci902()
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.TypeScript, "/repo/web", "/repo/web/index.ts"));

        var analyzer = new FakeAnalyzer(
            SupportedLanguage.TypeScript,
            (_, _) => Task.FromResult(
                AnalyzerExecutionResult.Failed(
                    AnalyzerFailureKind.ProcessFailure,
                    "helper exited with code 1")));

        var orchestrator = new AnalyzerOrchestrator([analyzer]);

        AnalysisOrchestrationResult result =
            await orchestrator.AnalyzeAsync(discovery);

        CheckerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ACI902", diagnostic.RuleId);
        Assert.Equal(DiagnosticSeverity.Danger, diagnostic.Severity);
        Assert.False(result.IsComplete);
    }

    [Theory]
    [InlineData(AnalyzerFailureKind.AnalysisFailure)]
    [InlineData(AnalyzerFailureKind.SemanticModelFailure)]
    [InlineData(AnalyzerFailureKind.GraphResolutionFailure)]
    public async Task AnalyzerCannotAnalyzeSupportedScopeBecomesAci001(
        AnalyzerFailureKind failureKind)
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.CSharp, "/repo/app", "/repo/app/App.cs"));

        var analyzer = new FakeAnalyzer(
            SupportedLanguage.CSharp,
            (_, _) => Task.FromResult(
                AnalyzerExecutionResult.Failed(
                    failureKind,
                    "scope could not be analyzed")));

        var orchestrator = new AnalyzerOrchestrator([analyzer]);

        AnalysisOrchestrationResult result =
            await orchestrator.AnalyzeAsync(discovery);

        CheckerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ACI001", diagnostic.RuleId);
        Assert.Equal(DiagnosticSeverity.Danger, diagnostic.Severity);
    }

    [Fact]
    public async Task PartialAnalyzerResultKeepsDiagnosticsAndAddsAci903()
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.Go, "/repo/go", "/repo/go/main.go"));

        var analyzer = new FakeAnalyzer(
            SupportedLanguage.Go,
            (_, _) => Task.FromResult(
                AnalyzerExecutionResult.Partial(
                    "one package could not be resolved",
                    [
                        new CheckerDiagnostic(
                            "ACI201",
                            DiagnosticSeverity.Notice,
                            "available diagnostic",
                            "/repo/go/main.go",
                            Language: "go")
                    ])));

        var orchestrator = new AnalyzerOrchestrator([analyzer]);

        AnalysisOrchestrationResult result =
            await orchestrator.AnalyzeAsync(discovery);

        Assert.False(result.IsComplete);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI201");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI903");
    }

    [Fact]
    public async Task ParallelModeOverlapsSynchronousAnalyzerWork()
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.Python, "/repo/one", "/repo/one/app.py"),
            Group(SupportedLanguage.Python, "/repo/two", "/repo/two/app.py"));
        using var started = new CountdownEvent(2);
        int overlappingScopes = 0;
        var analyzer = new FakeAnalyzer(
            SupportedLanguage.Python,
            (_, cancellationToken) =>
            {
                started.Signal();
                if (started.Wait(TimeSpan.FromSeconds(15), cancellationToken))
                {
                    Interlocked.Increment(ref overlappingScopes);
                }

                return Task.FromResult(AnalyzerExecutionResult.Complete());
            });
        var orchestrator = new AnalyzerOrchestrator([analyzer]);

        AnalysisOrchestrationResult result = await orchestrator.AnalyzeAsync(
            discovery,
            AnalyzerExecutionMode.Parallel);

        Assert.True(result.IsComplete);
        Assert.Equal(2, overlappingScopes);
    }

    [Theory]
    [InlineData(AnalyzerExecutionMode.Parallel)]
    [InlineData(AnalyzerExecutionMode.Sequential)]
    public async Task ExecutionModeControlsWhenAsyncScopesStart(AnalyzerExecutionMode mode)
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.CSharp, "/repo/one", "/repo/one/App.cs"),
            Group(SupportedLanguage.Python, "/repo/two", "/repo/two/app.py"));
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new FakeAnalyzer(
            SupportedLanguage.CSharp,
            async (_, cancellationToken) =>
            {
                firstStarted.SetResult();
                await releaseFirst.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                return AnalyzerExecutionResult.Complete();
            });
        var second = new FakeAnalyzer(
            SupportedLanguage.Python,
            (_, _) =>
            {
                secondStarted.SetResult();
                return Task.FromResult(AnalyzerExecutionResult.Complete());
            });
        var orchestrator = new AnalyzerOrchestrator([first, second]);
        Task<AnalysisOrchestrationResult> analysis = orchestrator.AnalyzeAsync(discovery, mode);

        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (mode == AnalyzerExecutionMode.Parallel)
            {
                await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            else
            {
                Assert.False(secondStarted.Task.IsCompleted);
            }

            Assert.False(analysis.IsCompleted);
        }
        finally
        {
            releaseFirst.TrySetResult();
        }

        AnalysisOrchestrationResult result = await analysis.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(secondStarted.Task.IsCompletedSuccessfully);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task SequentialModeRunsScopesInDiscoveryOrder()
    {
        var invocationOrder = new List<string>();
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.CSharp, "/repo/one", "/repo/one/App.cs"),
            Group(SupportedLanguage.Python, "/repo/two", "/repo/two/app.py"));

        var csharp = new FakeAnalyzer(
            SupportedLanguage.CSharp,
            (request, _) =>
            {
                invocationOrder.Add(request.SourceGroup.BoundaryPath);
                return Task.FromResult(AnalyzerExecutionResult.Complete());
            });
        var python = new FakeAnalyzer(
            SupportedLanguage.Python,
            (request, _) =>
            {
                invocationOrder.Add(request.SourceGroup.BoundaryPath);
                return Task.FromResult(AnalyzerExecutionResult.Complete());
            });

        var orchestrator = new AnalyzerOrchestrator([csharp, python]);

        await orchestrator.AnalyzeAsync(
            discovery,
            AnalyzerExecutionMode.Sequential);

        Assert.Collection(
            invocationOrder,
            path => Assert.Equal("/repo/one", path),
            path => Assert.Equal("/repo/two", path));
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.Python, "/repo/python", "/repo/python/app.py"));

        var analyzer = new FakeAnalyzer(
            SupportedLanguage.Python,
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return AnalyzerExecutionResult.Complete();
            });
        var orchestrator = new AnalyzerOrchestrator([analyzer]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => orchestrator.AnalyzeAsync(
                discovery,
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task CancellationAfterAnalyzerStartsIsPropagated()
    {
        AnalysisDiscoveryResult discovery = CreateDiscovery(
            Group(SupportedLanguage.Python, "/repo/python", "/repo/python/app.py"));
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var analyzer = new FakeAnalyzer(
            SupportedLanguage.Python,
            async (_, cancellationToken) =>
            {
                started.SetResult(cancellationToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return AnalyzerExecutionResult.Complete();
            });
        var orchestrator = new AnalyzerOrchestrator([analyzer]);
        using var cancellation = new CancellationTokenSource();

        Task<AnalysisOrchestrationResult> analysis = orchestrator.AnalyzeAsync(
            discovery,
            cancellationToken: cancellation.Token);
        Assert.Equal(cancellation.Token, await started.Task.WaitAsync(TimeSpan.FromSeconds(15)));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => analysis.WaitAsync(TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void DuplicateAnalyzerRegistrationIsRejected()
    {
        ILanguageAnalyzer first = FakeAnalyzer.Complete(SupportedLanguage.Go);
        ILanguageAnalyzer second = FakeAnalyzer.Complete(SupportedLanguage.Go);

        Assert.Throws<ArgumentException>(
            () => new AnalyzerOrchestrator([first, second]));
    }

    private static AnalysisDiscoveryResult CreateDiscovery(
        params DiscoveredSourceGroup[] groups) =>
        new(
            "/repo",
            AnalysisInputKind.Directory,
            groups,
            []);

    private static DiscoveredSourceGroup Group(
        SupportedLanguage language,
        string boundary,
        params string[] files) =>
        new(language, boundary, files);

    private sealed class FakeAnalyzer : ILanguageAnalyzer
    {
        private readonly Func<
            AnalyzerRequest,
            CancellationToken,
            Task<AnalyzerExecutionResult>> implementation;

        public FakeAnalyzer(
            SupportedLanguage language,
            Func<
                AnalyzerRequest,
                CancellationToken,
                Task<AnalyzerExecutionResult>> implementation)
        {
            Language = language;
            this.implementation = implementation;
        }

        public SupportedLanguage Language { get; }

        public Task<AnalyzerExecutionResult> AnalyzeAsync(
            AnalyzerRequest request,
            CancellationToken cancellationToken) =>
            implementation(request, cancellationToken);

        public static FakeAnalyzer Complete(
            SupportedLanguage language,
            params CheckerDiagnostic[] diagnostics) =>
            new(
                language,
                (_, _) => Task.FromResult(
                    AnalyzerExecutionResult.Complete(diagnostics)));
    }
}
