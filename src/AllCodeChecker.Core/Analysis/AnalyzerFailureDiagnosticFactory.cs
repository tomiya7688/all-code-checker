using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;

namespace AllCodeChecker.Analysis;

internal static class AnalyzerFailureDiagnosticFactory
{
    public static CheckerDiagnostic MissingAnalyzer(DiscoveredSourceGroup group) =>
        Create(
            "ACI903",
            group,
            $"{LanguageName(group.Language)} Analyzerが登録されていないため、対象scopeを解析できませんでした。");

    public static CheckerDiagnostic FromExecutionResult(
        DiscoveredSourceGroup group,
        AnalyzerExecutionResult result)
    {
        string reason = result.FailureReason
            ?? "Analyzerが理由を返さずに解析を完了できませんでした。";

        if (result.Status == AnalyzerCompletionStatus.Partial)
        {
            return Create(
                "ACI903",
                group,
                $"対象scopeの一部しか解析できませんでした: {reason}");
        }

        string ruleId = result.FailureKind switch
        {
            AnalyzerFailureKind.ProcessFailure => "ACI902",
            AnalyzerFailureKind.InternalError => "ACI901",
            AnalyzerFailureKind.AnalysisFailure => "ACI001",
            AnalyzerFailureKind.SemanticModelFailure => "ACI001",
            AnalyzerFailureKind.GraphResolutionFailure => "ACI001",
            null => "ACI901",
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                result.FailureKind,
                null)
        };

        return Create(ruleId, group, reason);
    }

    public static CheckerDiagnostic FromUnhandledException(
        DiscoveredSourceGroup group,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Create(
            "ACI901",
            group,
            $"{LanguageName(group.Language)} Analyzerで未処理例外が発生しました: {exception.GetType().Name}: {exception.Message}");
    }

    private static CheckerDiagnostic Create(
        string ruleId,
        DiscoveredSourceGroup group,
        string reason) =>
        new(
            ruleId,
            DiagnosticSeverity.Danger,
            reason,
            Path: group.BoundaryPath,
            Language: LanguageName(group.Language));

    private static string LanguageName(SupportedLanguage language) =>
        language switch
        {
            SupportedLanguage.CSharp => "csharp",
            SupportedLanguage.TypeScript => "typescript",
            SupportedLanguage.Python => "python",
            SupportedLanguage.Go => "go",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };
}
