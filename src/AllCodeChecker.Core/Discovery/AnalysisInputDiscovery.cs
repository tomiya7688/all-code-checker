namespace AllCodeChecker.Discovery;

public sealed class AnalysisInputDiscovery
{
    private readonly DiscoveryPathPolicy pathPolicy;

    public AnalysisInputDiscovery()
        : this(new DiscoveryPathPolicy())
    {
    }

    public AnalysisInputDiscovery(DiscoveryPathPolicy pathPolicy)
    {
        this.pathPolicy = pathPolicy ?? throw new ArgumentNullException(nameof(pathPolicy));
    }

    public AnalysisDiscoveryResult Discover(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        string fullPath = Path.GetFullPath(inputPath);

        if (Directory.Exists(fullPath))
        {
            return DiscoverDirectory(fullPath);
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("解析対象が見つかりません。", fullPath);
        }

        if (ProjectDefinitionDetector.IsProjectDefinition(fullPath))
        {
            return DiscoverProjectDefinition(fullPath);
        }

        if (!SourceLanguageDetector.TryDetect(fullPath, out SupportedLanguage language))
        {
            throw new UnsupportedInputException($"未対応の入力です: {fullPath}");
        }

        string boundary = FindNearestBoundary(Path.GetDirectoryName(fullPath)!, language)
            ?? Path.GetDirectoryName(fullPath)!;

        return new AnalysisDiscoveryResult(
            fullPath,
            AnalysisInputKind.SourceFile,
            [new DiscoveredSourceGroup(language, boundary, [fullPath])],
            [],
            EntryFile: fullPath);
    }

    private AnalysisDiscoveryResult DiscoverDirectory(string root)
    {
        IReadOnlyList<string> projectDefinitions = EnumerateFiles(root)
            .Where(ProjectDefinitionDetector.IsProjectDefinition)
            .ToArray();

        var groups = new Dictionary<(SupportedLanguage Language, string Boundary), List<string>>();
        var unsupportedFiles = new List<string>();

        foreach (string file in EnumerateFiles(root))
        {
            if (ProjectDefinitionDetector.IsProjectDefinition(file))
            {
                continue;
            }

            if (!SourceLanguageDetector.TryDetect(file, out SupportedLanguage language))
            {
                continue;
            }

            string boundary = FindNearestBoundaryFromKnownDefinitions(
                file,
                root,
                language,
                projectDefinitions);

            var key = (language, boundary);

            if (!groups.TryGetValue(key, out List<string>? sourceFiles))
            {
                sourceFiles = [];
                groups.Add(key, sourceFiles);
            }

            sourceFiles.Add(file);
        }

        DiscoveredSourceGroup[] discoveredGroups = groups
            .OrderBy(pair => pair.Key.Boundary, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Language)
            .Select(pair => new DiscoveredSourceGroup(
                pair.Key.Language,
                pair.Key.Boundary,
                pair.Value.OrderBy(path => path, StringComparer.Ordinal).ToArray()))
            .ToArray();

        return new AnalysisDiscoveryResult(
            root,
            AnalysisInputKind.Directory,
            discoveredGroups,
            unsupportedFiles);
    }

    private AnalysisDiscoveryResult DiscoverProjectDefinition(string projectDefinition)
    {
        string projectRoot = Path.GetDirectoryName(projectDefinition)!;
        SupportedLanguage[] supportedLanguages = Enum.GetValues<SupportedLanguage>()
            .Where(language => ProjectDefinitionDetector.SupportsLanguage(projectDefinition, language))
            .ToArray();

        if (supportedLanguages.Length == 0)
        {
            throw new UnsupportedInputException(
                $"このproject定義はv1 language analyzerへ直接割り当てできません: {projectDefinition}");
        }

        DiscoveredSourceGroup[] groups = supportedLanguages
            .Select(language => new DiscoveredSourceGroup(
                language,
                projectRoot,
                EnumerateFiles(projectRoot)
                    .Where(file => SourceLanguageDetector.TryDetect(file, out SupportedLanguage detected)
                        && detected == language)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()))
            .Where(group => group.SourceFiles.Count > 0)
            .ToArray();

        return new AnalysisDiscoveryResult(
            projectDefinition,
            AnalysisInputKind.ProjectDefinition,
            groups,
            []);
    }

    private IEnumerable<string> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string current = pending.Pop();

            foreach (string directory in Directory.EnumerateDirectories(current)
                         .OrderByDescending(path => path, StringComparer.Ordinal))
            {
                if (!pathPolicy.IsExcludedDirectory(root, directory))
                {
                    pending.Push(directory);
                }
            }

            foreach (string file in Directory.EnumerateFiles(current)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!pathPolicy.IsExcludedFile(root, file))
                {
                    yield return file;
                }
            }
        }
    }

    private static string FindNearestBoundaryFromKnownDefinitions(
        string sourceFile,
        string root,
        SupportedLanguage language,
        IReadOnlyList<string> projectDefinitions)
    {
        string? current = Path.GetDirectoryName(sourceFile);

        while (current is not null && IsInsideOrSame(current, root))
        {
            if (projectDefinitions.Any(project =>
                    string.Equals(Path.GetDirectoryName(project), current, StringComparison.Ordinal)
                    && ProjectDefinitionDetector.SupportsLanguage(project, language)))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        return root;
    }

    private static string? FindNearestBoundary(string startDirectory, SupportedLanguage language)
    {
        string? current = startDirectory;

        while (current is not null)
        {
            if (Directory.EnumerateFiles(current)
                .Any(file => ProjectDefinitionDetector.IsProjectDefinition(file)
                    && ProjectDefinitionDetector.SupportsLanguage(file, language)))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static bool IsInsideOrSame(string candidate, string root)
    {
        string relative = Path.GetRelativePath(root, candidate);

        return relative == "."
            || (!relative.StartsWith("..", StringComparison.Ordinal)
                && !Path.IsPathRooted(relative));
    }
}
