using AllCodeChecker.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CheckerSeverity = AllCodeChecker.Diagnostics.DiagnosticSeverity;

namespace AllCodeChecker.Analysis;

internal static class CSharpSyntaxAnalyzer
{
    public static (IReadOnlyList<CheckerDiagnostic> Diagnostics, IReadOnlyList<SyntaxTree> Trees)
        Parse(IEnumerable<string> files, CancellationToken cancellationToken)
    {
        var diagnostics = new List<CheckerDiagnostic>();
        var trees = new List<SyntaxTree>();
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string source = File.ReadAllText(file);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: file, cancellationToken: cancellationToken);
                trees.Add(tree);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new CheckerDiagnostic("ACI001", CheckerSeverity.Warning,
                    $"C# sourceを読み込めませんでした: {exception.Message}", file, Language: "csharp"));
            }
        }

        return (diagnostics, trees);
    }
}
