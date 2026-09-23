using AllCodeChecker.Discovery;
using Xunit;

namespace AllCodeChecker.Core.Tests;

public sealed class AnalysisInputDiscoveryTests
{
    [Fact]
    public void DirectoryDiscoveryKeepsSiblingProjectsSeparate()
    {
        using var fixture = new DiscoveryFixture();
        string first = fixture.CreateFile("apps/first/first.csproj", "<Project />");
        string second = fixture.CreateFile("apps/second/second.csproj", "<Project />");
        string firstSource = fixture.CreateFile("apps/first/Program.cs", "class First {}");
        string secondSource = fixture.CreateFile("apps/second/Program.cs", "class Second {}");

        var discovery = new AnalysisInputDiscovery();
        AnalysisDiscoveryResult result = discovery.Discover(fixture.Root);

        DiscoveredSourceGroup[] csharpGroups = result.Groups
            .Where(group => group.Language == SupportedLanguage.CSharp)
            .ToArray();

        Assert.Equal(2, csharpGroups.Length);
        Assert.Contains(csharpGroups, group =>
            group.BoundaryPath == Path.GetDirectoryName(first)
            && group.SourceFiles.SequenceEqual([firstSource]));
        Assert.Contains(csharpGroups, group =>
            group.BoundaryPath == Path.GetDirectoryName(second)
            && group.SourceFiles.SequenceEqual([secondSource]));
    }

    [Fact]
    public void DirectoryDiscoveryFindsMultipleLanguages()
    {
        using var fixture = new DiscoveryFixture();
        fixture.CreateFile("app/app.csproj", "<Project />");
        fixture.CreateFile("app/App.cs", "class App {}");
        fixture.CreateFile("web/tsconfig.json", "{}");
        fixture.CreateFile("web/index.ts", "export const value = 1;");
        fixture.CreateFile("tools/pyproject.toml", "[project]");
        fixture.CreateFile("tools/tool.py", "value = 1");
        fixture.CreateFile("worker/go.mod", "module example.test/worker");
        fixture.CreateFile("worker/main.go", "package main");

        var discovery = new AnalysisInputDiscovery();
        AnalysisDiscoveryResult result = discovery.Discover(fixture.Root);

        SupportedLanguage[] languages = result.Groups
            .Select(group => group.Language)
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal(
            [
                SupportedLanguage.CSharp,
                SupportedLanguage.TypeScript,
                SupportedLanguage.Python,
                SupportedLanguage.Go
            ],
            languages);
    }

    [Fact]
    public void DefaultPolicyExcludesGeneratedVendorAndBuildDirectories()
    {
        using var fixture = new DiscoveryFixture();
        string source = fixture.CreateFile("src/main.go", "package main");
        fixture.CreateFile("vendor/dependency.go", "package dependency");
        fixture.CreateFile("generated/output.go", "package generated");
        fixture.CreateFile("bin/copied.go", "package copied");

        var discovery = new AnalysisInputDiscovery();
        AnalysisDiscoveryResult result = discovery.Discover(fixture.Root);

        DiscoveredSourceGroup group = Assert.Single(result.Groups);
        Assert.Equal(SupportedLanguage.Go, group.Language);
        Assert.Equal(new[] { source }, group.SourceFiles);
    }

    [Fact]
    public void SingleSourceFilePreservesEntryPointAndNearestBoundary()
    {
        using var fixture = new DiscoveryFixture();
        fixture.CreateFile("web/tsconfig.json", "{}");
        string entry = fixture.CreateFile("web/src/index.ts", "export const value = 1;");

        var discovery = new AnalysisInputDiscovery();
        AnalysisDiscoveryResult result = discovery.Discover(entry);

        Assert.Equal(AnalysisInputKind.SourceFile, result.InputKind);
        Assert.Equal(entry, result.EntryFile);
        DiscoveredSourceGroup group = Assert.Single(result.Groups);
        Assert.Equal(SupportedLanguage.TypeScript, group.Language);
        Assert.Equal(Path.Combine(fixture.Root, "web"), group.BoundaryPath);
        Assert.Equal([entry], group.SourceFiles);
    }

    [Fact]
    public void UnsupportedSingleFileIsNotReportedAsSuccessfulAnalysis()
    {
        using var fixture = new DiscoveryFixture();
        string unsupported = fixture.CreateFile("src/main.rb", "puts 'hello'");

        var discovery = new AnalysisInputDiscovery();

        Assert.Throws<UnsupportedInputException>(() => discovery.Discover(unsupported));
    }

    private sealed class DiscoveryFixture : IDisposable
    {
        public DiscoveryFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"all-code-checker-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateFile(string relativePath, string content)
        {
            string path = Path.GetFullPath(Path.Combine(Root, relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
