using System.Text.Json;
using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;
using FastParse;
using CheckerSeverity = AllCodeChecker.Diagnostics.DiagnosticSeverity;

namespace AllCodeChecker.Analysis;

public sealed class PythonProjectAnalyzer : ILanguageAnalyzer
{
    private const FastParseField SyntaxFields =
        FastParseField.Id
        | FastParseField.ParentId
        | FastParseField.Rule
        | FastParseField.Text
        | FastParseField.Range
        | FastParseField.ChildCount
        | FastParseField.FieldName
        | FastParseField.Diagnostics;

    public SupportedLanguage Language => SupportedLanguage.Python;

    public Task<AnalyzerExecutionResult> AnalyzeAsync(
        AnalyzerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string[] files = SelectFiles(request);
        if (files.Length == 0)
        {
            return Task.FromResult(AnalyzerExecutionResult.Partial(
                "対象scopeに解析可能なPython sourceがありません.",
                [LoadFailure(request.SourceGroup.BoundaryPath, "Python sourceがありません.")]));
        }

        string root = request.InputPath is not null && Directory.Exists(request.InputPath)
            ? Path.GetFullPath(request.InputPath)
            : Path.GetFullPath(request.SourceGroup.BoundaryPath);
        var parsedModules = new List<ParsedModule>(files.Length);
        var diagnostics = new List<CheckerDiagnostic>();
        bool partial = false;

        using var parser = new FastParseClient();
        try
        {
            parser.LoadBundledLanguage("python");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Task.FromResult(AnalyzerExecutionResult.Partial(
                "Python文法を初期化できませんでした.",
                [LoadFailure(request.InputPath ?? request.SourceGroup.BoundaryPath, exception.Message)]));
        }

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string source = File.ReadAllText(file);
                ParseResult parsed = parser.ParseText(source, new ParseOptions
                {
                    Language = "python",
                    Format = FastParseFormat.Json,
                    Fields = SyntaxFields,
                    IncludeTokens = true
                });
                using JsonDocument syntax = JsonDocument.Parse(parsed.Text);
                JsonElement rootElement = syntax.RootElement;
                SyntaxNode[] nodes = ReadNodes(rootElement);
                SyntaxNode[] errors = nodes.Where(node => node.IsError || node.IsMissing).ToArray();
                foreach (SyntaxNode error in errors)
                {
                    diagnostics.Add(new CheckerDiagnostic(
                        "ACI002",
                        CheckerSeverity.Danger,
                        error.IsMissing
                            ? $"Python構文要素が不足しています: {error.Type}"
                            : $"Python構文を解析できません: {Trim(error.Text)}",
                        file,
                        error.StartLine,
                        error.StartColumn + 1,
                        "python"));
                }

                bool hasErrors = rootElement.GetProperty("hasErrors").GetBoolean();
                if (hasErrors && errors.Length == 0)
                {
                    SyntaxNode location = nodes.FirstOrDefault(node => node.HasError)
                        ?? nodes[0];
                    diagnostics.Add(new CheckerDiagnostic(
                        "ACI002",
                        CheckerSeverity.Danger,
                        "Python構文を解析できませんでした。",
                        file,
                        location.StartLine,
                        location.StartColumn + 1,
                        "python"));
                }

                partial |= hasErrors;
                parsedModules.Add(AnalyzeModule(file, root, nodes));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                partial = true;
                diagnostics.Add(LoadFailure(file, exception.Message));
            }
        }

        PythonModuleAnalysisData[] modules = ResolveImports(parsedModules);
        AnalyzerAnalysisData[] data = modules;
        return Task.FromResult(partial
            ? AnalyzerExecutionResult.Partial("Python sourceの一部を解析できませんでした.", diagnostics, data)
            : AnalyzerExecutionResult.Complete(diagnostics, data));
    }

    private static ParsedModule AnalyzeModule(string file, string root, SyntaxNode[] nodes)
    {
        string moduleName = GetModuleName(file, root);
        var children = nodes.Where(node => node.ParentId is not null)
            .GroupBy(node => node.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var nodesById = nodes.ToDictionary(node => node.Id);
        var imports = new List<PythonImportSummary>();
        var scopes = new List<PythonScopeSummary>();
        var frameworks = new HashSet<string>(StringComparer.Ordinal);
        var assignments = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        bool hasTestNamedFunction = false;

        foreach (SyntaxNode node in nodes)
        {
            if (node.Type is "import_statement" or "import_from_statement")
            {
                string imported = GetImportedName(node, nodes, children);
                if (imported.Length > 0)
                {
                    imports.Add(new PythonImportSummary(imported, null, node.StartLine));
                    if (imported == "pytest" || imported.StartsWith("pytest.", StringComparison.Ordinal))
                    {
                        frameworks.Add("pytest");
                    }

                    if (imported == "unittest" || imported.StartsWith("unittest.", StringComparison.Ordinal))
                    {
                        frameworks.Add("unittest");
                    }
                }
            }
            else if (node.Type is "function_definition" or "class_definition")
            {
                scopes.Add(SummarizeScope(node, nodesById, children));
                if (node.Type == "function_definition"
                    && GetFieldChild(node, "name", nodes, children)?.Text.StartsWith("test_", StringComparison.Ordinal) == true)
                {
                    hasTestNamedFunction = true;
                }
            }
            else if (node.Type is "assignment" or "augmented_assignment" or "named_expression")
            {
                SyntaxNode? left = GetFieldChild(node, "left", nodes, children)
                    ?? GetFieldChild(node, "name", nodes, children);
                SyntaxNode? right = GetFieldChild(node, "right", nodes, children)
                    ?? GetFieldChild(node, "value", nodes, children);
                if (left?.Type == "identifier" && right is not null)
                {
                    string scope = FindEnclosingScope(node, nodesById);
                    string variable = $"{scope}.{left.Text}";
                    if (!assignments.TryGetValue(variable, out HashSet<string>? types))
                    {
                        types = new HashSet<string>(StringComparer.Ordinal);
                        assignments.Add(variable, types);
                    }

                    types.Add(InferExpressionKind(right.Type));
                }
            }
        }

        string[] dynamicChanges = assignments
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key}: {string.Join(" -> ", pair.Value.Order(StringComparer.Ordinal))}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (hasTestNamedFunction && !frameworks.Contains("unittest"))
        {
            frameworks.Add("pytest");
        }

        return new ParsedModule(
            file,
            moduleName,
            imports,
            scopes,
            frameworks.Order(StringComparer.Ordinal).ToArray(),
            dynamicChanges);
    }

    private static PythonScopeSummary SummarizeScope(
        SyntaxNode scope,
        IReadOnlyDictionary<long, SyntaxNode> nodesById,
        IReadOnlyDictionary<long, SyntaxNode[]> children)
    {
        SyntaxNode[] scopeNodes = ScopeDescendants(scope, children).ToArray();
        SyntaxNode? name = scopeNodes.FirstOrDefault(node => node.ParentId == scope.Id && node.FieldName == "name");
        SyntaxNode? parameters = scopeNodes.FirstOrDefault(node => node.ParentId == scope.Id && node.FieldName == "parameters");
        string[] parameterNames = parameters is null
            ? []
            : Descendants(parameters, children)
                .Where(node => node.Type == "identifier")
                .Select(node => node.Text)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        SyntaxNode[] returns = scopeNodes.Where(node => node.Type == "return_statement").ToArray();
        bool isFunction = scope.Type == "function_definition";
        bool returnsNone = isFunction && returns.Any(node => GetChildren(node, children).Length == 0
            || GetChildren(node, children).Any(child => child.Type == "none"));
        bool returnsValue = isFunction && returns.Any(node => GetChildren(node, children).Any(child => child.Type != "none"));
        string[] assignedNames = scopeNodes
            .Where(node => node.Type is "assignment" or "augmented_assignment")
            .Select(node => GetFieldChild(node, "left", nodesById.Values, children))
            .Where(node => node is not null)
            .SelectMany(node => Descendants(node!, children))
            .Where(node => node.Type == "identifier")
            .Select(node => node.Text)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new PythonScopeSummary(
            isFunction ? "function" : "class",
            name?.Text ?? "<unknown>",
            scope.StartLine,
            scope.EndLine,
            parameterNames,
            assignedNames,
            returnsNone,
            returnsValue);
    }

    private static IEnumerable<SyntaxNode> ScopeDescendants(
        SyntaxNode root,
        IReadOnlyDictionary<long, SyntaxNode[]> children)
    {
        var pending = new Stack<SyntaxNode>();
        foreach (SyntaxNode child in GetChildren(root, children).Reverse())
        {
            pending.Push(child);
        }

        while (pending.Count > 0)
        {
            SyntaxNode current = pending.Pop();
            yield return current;
            if (current.Type is "function_definition" or "class_definition")
            {
                continue;
            }

            foreach (SyntaxNode child in GetChildren(current, children).Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static PythonModuleAnalysisData[] ResolveImports(IReadOnlyList<ParsedModule> modules)
    {
        var moduleNames = new HashSet<string>(modules.Select(module => module.ModuleName), StringComparer.Ordinal);
        return modules.Select(module => new PythonModuleAnalysisData(
            module.FilePath,
            module.ModuleName,
            module.Imports.Select(import => import with
            {
                ResolvedProjectModule = ResolveModule(import.Name, module.FilePath, module.ModuleName, moduleNames)
            }).ToArray(),
            module.Scopes,
            module.TestFrameworks,
            module.DynamicTypeChanges)).ToArray();
    }

    private static string? ResolveModule(
        string importedName,
        string filePath,
        string currentModule,
        HashSet<string> moduleNames)
    {
        if (moduleNames.Contains(importedName))
        {
            return importedName;
        }

        string parent = currentModule.Contains('.') ? currentModule[..currentModule.LastIndexOf('.')] : string.Empty;
        if (importedName.Length > 0 && importedName[0] == '.')
        {
            bool isPackageInitializer = Path.GetFileName(filePath)
                .Equals("__init__.py", StringComparison.OrdinalIgnoreCase);
            string package = isPackageInitializer ? currentModule : parent;
            int level = importedName.TakeWhile(character => character == '.').Count();
            for (int index = 1; index < level && package.Length > 0; index++)
            {
                package = package.Contains('.') ? package[..package.LastIndexOf('.')] : string.Empty;
            }

            string suffix = importedName[level..];
            importedName = package.Length == 0 ? suffix : suffix.Length == 0 ? package : $"{package}.{suffix}";
            if (moduleNames.Contains(importedName))
            {
                return importedName;
            }
        }

        string candidate = parent.Length == 0 ? importedName : $"{parent}.{importedName}";
        return moduleNames.Contains(candidate) ? candidate : null;
    }

    private static string GetImportedName(
        SyntaxNode import,
        SyntaxNode[] nodes,
        IReadOnlyDictionary<long, SyntaxNode[]> children)
    {
        string field = import.Type == "import_from_statement" ? "module_name" : "name";
        SyntaxNode? module = GetFieldChild(import, field, nodes, children);
        return module?.Text ?? string.Empty;
    }

    private static SyntaxNode? GetFieldChild(
        SyntaxNode parent,
        string fieldName,
        IEnumerable<SyntaxNode> nodes,
        IReadOnlyDictionary<long, SyntaxNode[]> children)
    {
        return GetChildren(parent, children).FirstOrDefault(child => child.FieldName == fieldName)
            ?? nodes.FirstOrDefault(node => node.ParentId == parent.Id && node.FieldName == fieldName);
    }

    private static SyntaxNode[] GetChildren(
        SyntaxNode parent,
        IReadOnlyDictionary<long, SyntaxNode[]> children) =>
        children.TryGetValue(parent.Id, out SyntaxNode[]? values) ? values : [];

    private static IEnumerable<SyntaxNode> Descendants(
        SyntaxNode root,
        IReadOnlyDictionary<long, SyntaxNode[]> children)
    {
        var pending = new Stack<SyntaxNode>();
        foreach (SyntaxNode child in GetChildren(root, children).Reverse())
        {
            pending.Push(child);
        }

        while (pending.Count > 0)
        {
            SyntaxNode current = pending.Pop();
            yield return current;
            foreach (SyntaxNode child in GetChildren(current, children).Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static string FindEnclosingScope(SyntaxNode node, IReadOnlyDictionary<long, SyntaxNode> nodesById)
    {
        long? parentId = node.ParentId;
        while (parentId is not null && nodesById.TryGetValue(parentId.Value, out SyntaxNode? parent))
        {
            if (parent.Type is "function_definition" or "class_definition")
            {
                SyntaxNode? name = nodesById.Values.FirstOrDefault(candidate => candidate.ParentId == parent.Id
                    && candidate.FieldName == "name");
                return name?.Text ?? parent.Type;
            }

            parentId = parent.ParentId;
        }

        return "<module>";
    }

    private static string InferExpressionKind(string type) => type switch
    {
        "integer" or "float" => "number",
        "string" or "concatenated_string" => "string",
        "true" or "false" => "bool",
        "none" => "None",
        "list" or "list_comprehension" => "list",
        "dictionary" or "dictionary_comprehension" => "dict",
        "set" or "set_comprehension" => "set",
        "tuple" => "tuple",
        "call" => "call",
        "identifier" => "unknown",
        _ => type
    };

    private static string GetModuleName(string file, string root)
    {
        string relative = Path.GetRelativePath(root, file);
        string withoutExtension = Path.ChangeExtension(relative, null) ?? relative;
        if (Path.GetFileName(withoutExtension).Equals("__init__", StringComparison.OrdinalIgnoreCase))
        {
            withoutExtension = Path.GetDirectoryName(withoutExtension) ?? string.Empty;
        }

        return withoutExtension.Replace(Path.DirectorySeparatorChar, '.')
            .Replace(Path.AltDirectorySeparatorChar, '.')
            .Trim('.');
    }

    private static SyntaxNode[] ReadNodes(JsonElement result)
    {
        return result.GetProperty("nodes").EnumerateArray().Select(node => new SyntaxNode(
            node.GetProperty("id").GetInt64(),
            node.GetProperty("parentId").ValueKind == JsonValueKind.Null
                ? null
                : node.GetProperty("parentId").GetInt64(),
            node.GetProperty("rule").GetString() ?? string.Empty,
            node.GetProperty("text").GetString() ?? string.Empty,
            node.GetProperty("startLine").GetInt32(),
            node.GetProperty("startColumn").GetInt32(),
            node.GetProperty("endLine").GetInt32(),
            node.GetProperty("endColumn").GetInt32(),
            node.GetProperty("fieldName").ValueKind == JsonValueKind.Null
                ? null
                : node.GetProperty("fieldName").GetString(),
            node.GetProperty("isError").GetBoolean(),
            node.GetProperty("isMissing").GetBoolean(),
            node.GetProperty("hasError").GetBoolean())).ToArray();
    }

    private static string[] SelectFiles(AnalyzerRequest request)
    {
        IEnumerable<string> files = request.SourceGroup.SourceFiles;
        if (request.EntryFile is not null)
        {
            string entry = Path.GetFullPath(request.EntryFile);
            files = files.Where(file => PathComparer.Equals(Path.GetFullPath(file), entry));
        }

        string[] selected = files.Where(File.Exists)
            .Where(file => Path.GetExtension(file).Equals(".py", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .Order(PathComparer)
            .ToArray();
        if (request.InputKind != AnalysisInputKind.ProjectDefinition
            || request.InputPath is null
            || !Path.GetFileName(request.InputPath).Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase))
        {
            return selected;
        }

        string projectRoot = Path.GetDirectoryName(Path.GetFullPath(request.InputPath))!;
        var hasNestedBoundary = new Dictionary<string, bool>(PathComparer);
        return selected.Where(file => !HasNestedProject(file, projectRoot, hasNestedBoundary)).ToArray();
    }

    private static bool HasNestedProject(string file, string projectRoot, Dictionary<string, bool> cache)
    {
        string? current = Path.GetDirectoryName(file);
        while (current is not null && !PathComparer.Equals(current, projectRoot))
        {
            if (!cache.TryGetValue(current, out bool hasProject))
            {
                hasProject = Directory.EnumerateFiles(current)
                    .Any(path => Path.GetFileName(path).Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase));
                cache.Add(current, hasProject);
            }

            if (hasProject)
            {
                return true;
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    private static CheckerDiagnostic LoadFailure(string path, string reason) =>
        new("ACI001", CheckerSeverity.Danger, $"Python解析に失敗しました: {reason}", path, Language: "python");

    private static string Trim(string text) => text.Length <= 120 ? text : text[..120];

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record ParsedModule(
        string FilePath,
        string ModuleName,
        IReadOnlyList<PythonImportSummary> Imports,
        IReadOnlyList<PythonScopeSummary> Scopes,
        IReadOnlyList<string> TestFrameworks,
        IReadOnlyList<string> DynamicTypeChanges);

    private sealed record SyntaxNode(
        long Id,
        long? ParentId,
        string Type,
        string Text,
        int StartLine,
        int StartColumn,
        int EndLine,
        int EndColumn,
        string? FieldName,
        bool IsError,
        bool IsMissing,
        bool HasError);
}
