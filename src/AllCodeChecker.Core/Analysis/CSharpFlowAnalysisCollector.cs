using AllCodeChecker.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using CheckerSeverity = AllCodeChecker.Diagnostics.DiagnosticSeverity;

namespace AllCodeChecker.Analysis;

internal static class CSharpFlowAnalysisCollector
{
    public static (IReadOnlyList<CSharpFlowAnalysisSummary> Summaries, IReadOnlyList<CheckerDiagnostic> Diagnostics)
        Collect(Compilation compilation, CancellationToken cancellationToken, IReadOnlyList<string>? sourceFiles = null)
    {
        var summaries = new List<CSharpFlowAnalysisSummary>();
        var diagnostics = new List<CheckerDiagnostic>();
        var paths = sourceFiles?.Select(Path.GetFullPath).ToHashSet(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (paths is not null && (string.IsNullOrEmpty(tree.FilePath) || !paths.Contains(Path.GetFullPath(tree.FilePath)))
                || CSharpGeneratedCodeDetector.IsGenerated(tree.FilePath, tree.GetText(cancellationToken).ToString()))
            {
                continue;
            }

            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (SyntaxNode declaration in tree.GetRoot(cancellationToken).DescendantNodesAndSelf())
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool isTopLevel = declaration is CompilationUnitSyntax topLevelUnit
                    && topLevelUnit.Members.Any(member => member is GlobalStatementSyntax);
                if (declaration is not (BaseMethodDeclarationSyntax or AccessorDeclarationSyntax) && !isTopLevel)
                {
                    continue;
                }

                SyntaxNode? body = GetBody(declaration);
                if (body is null)
                {
                    continue;
                }

                try
                {
                    ControlFlowGraph? graph = ControlFlowGraph.Create(declaration, model, cancellationToken);
                    if (graph is null)
                    {
                        AddFailure(tree, declaration, diagnostics, "control-flow graph was unavailable");
                        continue;
                    }

                    CollectGraph(graph, declaration, model, summaries, diagnostics, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    AddFailure(tree, declaration, diagnostics, exception.Message);
                }
            }
        }

        return (summaries, diagnostics);
    }

    private static void CollectGraph(
        ControlFlowGraph graph,
        SyntaxNode declaration,
        SemanticModel model,
        List<CSharpFlowAnalysisSummary> summaries,
        List<CheckerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SyntaxNode? body = GetBody(declaration);
        DataFlowAnalysis? flow;
        if (declaration is CompilationUnitSyntax unit)
        {
            GlobalStatementSyntax[] statements = unit.Members.OfType<GlobalStatementSyntax>().ToArray();
            flow = model.AnalyzeDataFlow(statements[0].Statement, statements[^1].Statement);
        }
        else
        {
            flow = body is null ? null : model.AnalyzeDataFlow(body);
        }
        if (flow is null || !flow.Succeeded)
        {
            AddFailure(declaration.SyntaxTree, declaration, diagnostics, "data-flow analysis was unavailable");
        }
        else
        {
            FileLinePositionSpan location = declaration.GetLocation().GetLineSpan();
            ISymbol? symbol = declaration is CompilationUnitSyntax
                ? model.Compilation.GetEntryPoint(cancellationToken)
                : model.GetDeclaredSymbol(declaration, cancellationToken)
                    ?? (model.GetOperation(declaration, cancellationToken) as IAnonymousFunctionOperation)?.Symbol;
            summaries.Add(new CSharpFlowAnalysisSummary(
                declaration.SyntaxTree.FilePath, location.StartLinePosition.Line + 1,
                location.EndLinePosition.Line + 1,
                symbol?.ToDisplayString() ?? declaration.Kind().ToString(),
                graph.Blocks.Select(block => new CSharpFlowBlockSummary(
                    block.Ordinal, block.Kind.ToString(),
                    block.FallThroughSuccessor?.Destination?.Ordinal,
                    block.ConditionalSuccessor?.Destination?.Ordinal)).ToArray(),
                Names(flow.VariablesDeclared), Names(flow.DataFlowsIn), Names(flow.DataFlowsOut),
                Names(flow.ReadInside), Names(flow.WrittenInside), Names(flow.AlwaysAssigned),
                Names(flow.Captured), Names(flow.ReadOutside), Names(flow.WrittenOutside)));
        }

        foreach (IMethodSymbol local in graph.LocalFunctions)
        {
            SyntaxNode localDeclaration = local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);
            CollectGraph(graph.GetLocalFunctionControlFlowGraph(local, cancellationToken),
                localDeclaration, model, summaries, diagnostics, cancellationToken);
        }

        foreach (IFlowAnonymousFunctionOperation anonymous in GraphOperations(graph).SelectMany(AnonymousFunctions))
        {
            CollectGraph(graph.GetAnonymousFunctionControlFlowGraph(anonymous, cancellationToken),
                anonymous.Syntax, model, summaries, diagnostics, cancellationToken);
        }
    }

    private static IEnumerable<IOperation> GraphOperations(ControlFlowGraph graph)
    {
        foreach (BasicBlock block in graph.Blocks)
        {
            foreach (IOperation operation in block.Operations)
            {
                yield return operation;
            }

            if (block.BranchValue is not null)
            {
                yield return block.BranchValue;
            }
        }
    }

    private static IEnumerable<IFlowAnonymousFunctionOperation> AnonymousFunctions(IOperation operation)
    {
        if (operation is IFlowAnonymousFunctionOperation anonymous)
        {
            yield return anonymous;
        }

        foreach (IOperation child in operation.ChildOperations)
        {
            foreach (IFlowAnonymousFunctionOperation nested in AnonymousFunctions(child))
            {
                yield return nested;
            }
        }
    }

    private static SyntaxNode? GetBody(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => method.Body ?? (SyntaxNode?)method.ExpressionBody?.Expression,
        AccessorDeclarationSyntax accessor => accessor.Body ?? (SyntaxNode?)accessor.ExpressionBody?.Expression,
        LocalFunctionStatementSyntax local => local.Body ?? (SyntaxNode?)local.ExpressionBody?.Expression,
        AnonymousFunctionExpressionSyntax anonymous => anonymous.Body,
        CompilationUnitSyntax unit => unit.Members.OfType<GlobalStatementSyntax>().FirstOrDefault()?.Statement,
        _ => null
    };

    private static string[] Names(IEnumerable<ISymbol> symbols) => symbols
        .Select(symbol => symbol.ToDisplayString())
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private static void AddFailure(
        SyntaxTree tree,
        SyntaxNode declaration,
        List<CheckerDiagnostic> diagnostics,
        string reason)
    {
        FileLinePositionSpan location = declaration.GetLocation().GetLineSpan();
        diagnostics.Add(new CheckerDiagnostic("ACI001", CheckerSeverity.Warning,
            $"C# flow解析に失敗しました: {reason}", tree.FilePath,
            location.StartLinePosition.Line + 1, location.StartLinePosition.Character + 1, "csharp"));
    }
}
