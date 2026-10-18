using System.Globalization;
using System.Collections.Concurrent;
using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using CheckerSeverity = AllCodeChecker.Diagnostics.DiagnosticSeverity;

namespace AllCodeChecker.Analysis;

public sealed class CSharpProjectAnalyzer : ILanguageAnalyzer
{
    private static readonly object LocatorLock = new();
    public SupportedLanguage Language => SupportedLanguage.CSharp;

    public async Task<AnalyzerExecutionResult> AnalyzeAsync(
        AnalyzerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        string[] definitions = FindDefinitions(request);
        if (definitions.Length == 0)
        {
            return AnalyzeLooseSources(request, cancellationToken);
        }

        var diagnostics = new List<CheckerDiagnostic>();
        var data = new List<AnalyzerAnalysisData>();
        string scopePath = request.InputPath ?? request.SourceGroup.BoundaryPath;
        try
        {
            RegisterMsBuild();
            using MSBuildWorkspace workspace = MSBuildWorkspace.Create();
            var workspaceFailures = new ConcurrentQueue<CheckerDiagnostic>();
            workspace.RegisterWorkspaceFailedHandler(args =>
            {
                if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    workspaceFailures.Enqueue(LoadDiagnostic(request, scopePath,
                        $"workspace: {args.Diagnostic.Message}"));
                }
            });
            bool partial = false;
            foreach (string definition in definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await OpenDefinitionAsync(workspace, definition, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
                {
                    partial = true;
                    diagnostics.Add(LoadDiagnostic(request, definition, exception.Message));
                }
            }

            Project[] projects = SelectProjects(workspace.CurrentSolution, request, definitions);
            if (projects.Length == 0)
            {
                diagnostics.AddRange(workspaceFailures);
                diagnostics.Add(LoadDiagnostic(request, scopePath,
                    "対象sourceをCompile itemに含むC# projectがありません。"));
                return AnalyzerExecutionResult.Partial("対象sourceに対応するC# projectがsolutionにありません。",
                    diagnostics);
            }

            foreach (Project project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    Compilation? raw = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                    if (raw is null)
                    {
                        partial = true;
                        diagnostics.Add(LoadDiagnostic(request, project.FilePath ?? scopePath,
                            $"{project.Name} compilationを作成できませんでした。"));
                        continue;
                    }

                    // Generated declarations must remain in the compilation for semantic binding.
                    AddCompilerDiagnostics(raw, diagnostics, request, cancellationToken);
                    var flow = CSharpFlowAnalysisCollector.Collect(raw, cancellationToken, request.SourceGroup.SourceFiles);
                    data.AddRange(flow.Summaries);
                    diagnostics.AddRange(flow.Diagnostics);
                    partial |= flow.Diagnostics.Count > 0;
                }
                catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
                {
                    partial = true;
                    diagnostics.Add(LoadDiagnostic(request, project.FilePath ?? scopePath, exception.Message));
                }
            }

            diagnostics.AddRange(workspaceFailures);
            partial |= !workspaceFailures.IsEmpty;

            return partial
                ? AnalyzerExecutionResult.Partial("C# projectの一部解析に失敗しました。", diagnostics, data)
                : AnalyzerExecutionResult.Complete(diagnostics, data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            diagnostics.Add(LoadDiagnostic(request, scopePath, exception.Message));
            return AnalyzerExecutionResult.Partial($"C# projectを読み込めませんでした: {exception.Message}",
                diagnostics, data);
        }
    }

    private static AnalyzerExecutionResult AnalyzeLooseSources(AnalyzerRequest request, CancellationToken token)
    {
        (IReadOnlyList<CheckerDiagnostic> parseDiagnostics, IReadOnlyList<SyntaxTree> trees) =
            CSharpSyntaxAnalyzer.Parse(request.SourceGroup.SourceFiles, token);
        if (trees.Count == 0)
        {
            return AnalyzerExecutionResult.Partial("解析可能なC# sourceがありません。", parseDiagnostics);
        }

        string[] tpa = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];
        MetadataReference[] references = tpa.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        bool hasTopLevelStatements = trees.Any(tree => tree.GetRoot(token) is CompilationUnitSyntax unit
            && unit.Members.Any(member => member is GlobalStatementSyntax));
        CSharpCompilation compilation = CSharpCompilation.Create("AllCodeChecker.LooseSources", trees, references,
            new CSharpCompilationOptions(hasTopLevelStatements ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var diagnostics = new List<CheckerDiagnostic>(parseDiagnostics);
        AddCompilerDiagnostics(compilation, diagnostics, request, token);
        var flow = CSharpFlowAnalysisCollector.Collect(compilation, token, request.SourceGroup.SourceFiles);
        diagnostics.AddRange(flow.Diagnostics);
        return AnalyzerExecutionResult.Partial(
            "project定義がないため、project固有の参照とbuild propertyを使った解析はできません。",
            diagnostics,
            flow.Summaries.Cast<AnalyzerAnalysisData>());
    }

    private static void RegisterMsBuild()
    {
        lock (LocatorLock)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }

    private static string[] FindDefinitions(AnalyzerRequest request)
    {
        if (request.InputKind == AnalysisInputKind.ProjectDefinition && request.InputPath is not null)
        {
            return [request.InputPath];
        }

        if (!Directory.Exists(request.SourceGroup.BoundaryPath))
        {
            return [];
        }

        string[] definitions = Directory.EnumerateFiles(request.SourceGroup.BoundaryPath)
            .Where(path => ProjectDefinitionDetector.SupportsLanguage(path, SupportedLanguage.CSharp))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        string[] projects = definitions.Where(path => Path.GetExtension(path)
            .Equals(".csproj", StringComparison.OrdinalIgnoreCase)).ToArray();
        // A directory scope may contain several independent project files.
        return projects.Length > 0 ? projects : definitions.Take(1).ToArray();
    }

    private static async Task OpenDefinitionAsync(
        MSBuildWorkspace workspace, string definition, CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(definition);
        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            await workspace.OpenSolutionAsync(definition, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else if (!workspace.CurrentSolution.Projects.Any(project => project.FilePath is not null
            && PathComparer.Equals(Path.GetFullPath(project.FilePath), Path.GetFullPath(definition))))
        {
            await workspace.OpenProjectAsync(definition, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static Project[] SelectProjects(Solution solution, AnalyzerRequest request, string[] definitions)
    {
        Project[] projects = solution.Projects.Where(project =>
            project.Language == LanguageNames.CSharp && project.FilePath is not null).ToArray();
        if (definitions.All(path => Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            var paths = definitions.Select(Path.GetFullPath).ToHashSet(PathComparer);
            projects = projects.Where(project => paths.Contains(Path.GetFullPath(project.FilePath!))).ToArray();
        }

        if (request.EntryFile is not null)
        {
            string entry = Path.GetFullPath(request.EntryFile);
            return projects.Where(project => project.Documents.Any(document => document.FilePath is not null
                && PathComparer.Equals(Path.GetFullPath(document.FilePath), entry))).ToArray();
        }

        string[] files = request.SourceGroup.SourceFiles.Select(Path.GetFullPath).ToArray();
        Project[] matching = projects.Where(project => project.Documents.Any(document =>
            document.FilePath is not null
            && files.Contains(Path.GetFullPath(document.FilePath), PathComparer))).ToArray();
        return matching;
    }

    private static void AddCompilerDiagnostics(
        Compilation compilation,
        List<CheckerDiagnostic> diagnostics,
        AnalyzerRequest request,
        CancellationToken cancellationToken)
    {
        var sourceFiles = request.SourceGroup.SourceFiles.Select(Path.GetFullPath).ToHashSet(PathComparer);
        var syntaxErrors = compilation.GetParseDiagnostics(cancellationToken)
            .Select(item => (item.Id, item.Location.SourceTree, item.Location.SourceSpan)).ToHashSet();
        foreach (Diagnostic diagnostic in compilation.GetDiagnostics(cancellationToken).Where(item =>
                     item.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error
                         or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning))
        {
            SyntaxTree? tree = diagnostic.Location.SourceTree;
            if (tree is not null && (string.IsNullOrEmpty(tree.FilePath) || !sourceFiles.Contains(Path.GetFullPath(tree.FilePath))
                || CSharpGeneratedCodeDetector.IsGenerated(tree.FilePath, tree.GetText(cancellationToken).ToString())))
            {
                continue;
            }

            string? ruleId = syntaxErrors.Contains((diagnostic.Id, tree, diagnostic.Location.SourceSpan))
                ? "ACI002" : MapCompilerDiagnostic(diagnostic);
            if (ruleId is null)
            {
                continue;
            }
            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            diagnostics.Add(new CheckerDiagnostic(ruleId,
                diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
                    ? CheckerSeverity.Danger : CheckerSeverity.Warning,
                $"{diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}",
                diagnostic.Location.IsInSource ? diagnostic.Location.SourceTree?.FilePath : null,
                diagnostic.Location.IsInSource ? span.StartLinePosition.Line + 1 : null,
                diagnostic.Location.IsInSource ? span.StartLinePosition.Character + 1 : null,
                "csharp"));
        }
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static string? MapCompilerDiagnostic(Diagnostic diagnostic)
    {
        if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
        {
            return diagnostic.Id.StartsWith("CS86", StringComparison.Ordinal) ? "ACI008" : null;
        }

        return diagnostic.Id switch
        {
            "CS0103" or "CS0246" or "CS0234" => "ACI003",
            "CS0029" or "CS0030" or "CS0266" or "CS1503" => "ACI004",
            "CS1061" or "CS0117" or "CS0122" => "ACI005",
            "CS1501" or "CS7036" or "CS1729" or "CS0123" => "ACI006",
            _ => "ACI001"
        };
    }

    private static CheckerDiagnostic LoadDiagnostic(AnalyzerRequest request, string path, string reason) =>
        new("ACI001", CheckerSeverity.Warning, $"C# project load error: {reason}", path, Language: "csharp");
}
