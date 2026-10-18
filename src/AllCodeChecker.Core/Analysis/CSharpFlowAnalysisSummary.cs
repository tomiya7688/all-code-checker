namespace AllCodeChecker.Analysis;

public sealed record CSharpFlowAnalysisSummary(
    string FilePath,
    int StartLine,
    int EndLine,
    string Symbol,
    IReadOnlyList<CSharpFlowBlockSummary> Blocks,
    IReadOnlyList<string> VariablesDeclared,
    IReadOnlyList<string> DataFlowsIn,
    IReadOnlyList<string> DataFlowsOut,
    IReadOnlyList<string> ReadInside,
    IReadOnlyList<string> WrittenInside,
    IReadOnlyList<string> AlwaysAssigned,
    IReadOnlyList<string> Captured,
    IReadOnlyList<string> ReadOutside,
    IReadOnlyList<string> WrittenOutside) : AnalyzerAnalysisData;

public sealed record CSharpFlowBlockSummary(
    int Ordinal,
    string Kind,
    int? FallThroughSuccessor,
    int? ConditionalSuccessor);
