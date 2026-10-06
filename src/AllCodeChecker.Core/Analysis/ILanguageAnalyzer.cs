using AllCodeChecker.Discovery;

namespace AllCodeChecker.Analysis;

public interface ILanguageAnalyzer
{
    SupportedLanguage Language { get; }

    Task<AnalyzerExecutionResult> AnalyzeAsync(
        AnalyzerRequest request,
        CancellationToken cancellationToken);
}
