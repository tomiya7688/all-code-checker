namespace AllCodeChecker.Analysis;

public sealed record TypeScriptProjectAnalysisData(
    string ProjectPath,
    bool StrictNullChecks,
    IReadOnlyList<TypeScriptSymbolSummary> Symbols,
    IReadOnlyList<string> TestScripts) : AnalyzerAnalysisData;

public sealed record TypeScriptSymbolSummary(
    string FilePath,
    string Name,
    string Type,
    int Line,
    bool IsAny,
    bool IsUnknown,
    bool IncludesNull,
    bool IncludesUndefined,
    IReadOnlyList<string> Properties);
