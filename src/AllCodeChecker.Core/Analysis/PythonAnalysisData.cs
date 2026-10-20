namespace AllCodeChecker.Analysis;

public sealed record PythonModuleAnalysisData(
    string FilePath,
    string ModuleName,
    IReadOnlyList<PythonImportSummary> Imports,
    IReadOnlyList<PythonScopeSummary> Scopes,
    IReadOnlyList<string> TestFrameworks,
    IReadOnlyList<string> DynamicTypeChanges) : AnalyzerAnalysisData;

public sealed record PythonImportSummary(
    string Name,
    string? ResolvedProjectModule,
    int Line);

public sealed record PythonScopeSummary(
    string Kind,
    string Name,
    int StartLine,
    int EndLine,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> AssignedNames,
    bool MayReturnNone,
    bool AlsoReturnsValue);
