using AllCodeChecker.Analysis;
using AllCodeChecker.Discovery;
using Xunit;

namespace AllCodeChecker.Core.Tests;

public sealed class PythonProjectAnalyzerTests
{
    [Fact]
    public async Task SyntaxErrorsAreReportedAtTheirSourceLocation()
    {
        using var fixture = new AnalyzerFixture();
        string source = fixture.Write("broken.py", "def broken(:\n    pass\n");

        var result = await AnalyzerFixture.AnalyzeAsync(source, new PythonProjectAnalyzer());

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI002"
            && diagnostic.Path == source
            && diagnostic.Line == 1);
        Assert.Contains(result.Scopes, scope => scope.Status == AnalyzerCompletionStatus.Partial);
    }

    [Fact]
    public async Task ProjectAnalysisCapturesImportsScopesTestsNoneAndDynamicAssignments()
    {
        using var fixture = new AnalyzerFixture();
        string project = fixture.Write("pyproject.toml", "[project]\nname = 'fixture'\n");
        fixture.Write("pkg/__init__.py", "");
        fixture.Write("pkg/models.py", "def lookup(flag):\n    if flag:\n        return None\n    return 1\n\nvalue = 1\nvalue = 'changed'\n");
        string app = fixture.Write("pkg/client.py", "import pytest\nimport unittest\nfrom .models import lookup\n\nclass TestSuite(unittest.TestCase):\n    def test_lookup(self):\n        return lookup(True)\n");

        var result = await AnalyzerFixture.AnalyzeAsync(project, new PythonProjectAnalyzer());
        PythonModuleAnalysisData appData = Assert.Single(result.Scopes
            .SelectMany(scope => scope.AnalysisData ?? [])
            .OfType<PythonModuleAnalysisData>(), module => module.FilePath == app);
        PythonModuleAnalysisData modelData = Assert.Single(result.Scopes
            .SelectMany(scope => scope.AnalysisData ?? [])
            .OfType<PythonModuleAnalysisData>(), module => module.ModuleName == "pkg.models");

        Assert.Contains("pytest", appData.TestFrameworks);
        Assert.Contains("unittest", appData.TestFrameworks);
        Assert.Contains(appData.Imports, import => import.Name == ".models"
            && import.ResolvedProjectModule == "pkg.models");
        Assert.Contains(modelData.Scopes, scope => scope.Name == "lookup"
            && scope.MayReturnNone
            && scope.AlsoReturnsValue);
        Assert.Contains(modelData.DynamicTypeChanges, change => change.Contains("value:", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI001"
            || diagnostic.RuleId == "ACI002");
    }

    [Fact]
    public async Task SingleFileAnalysisDoesNotIncludeNeighboringModuleDiagnostics()
    {
        using var fixture = new AnalyzerFixture();
        fixture.Write("good.py", "value = 1\n");
        string bad = fixture.Write("bad.py", "def broken(:\n    pass\n");

        var discovery = new AnalysisInputDiscovery().Discover(bad);
        var result = await new AnalyzerOrchestrator([new PythonProjectAnalyzer()]).AnalyzeAsync(discovery);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI002" && diagnostic.Path == bad);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Path?.EndsWith("good.py", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ProjectDefinitionDoesNotCrossNestedPythonProjectBoundary()
    {
        using var fixture = new AnalyzerFixture();
        string project = fixture.Write("pyproject.toml", "[project]\nname = 'outer'\n");
        fixture.Write("app.py", "value = 1\n");
        fixture.Write("nested/pyproject.toml", "[project]\nname = 'inner'\n");
        fixture.Write("nested/inner.py", "value = 2\n");

        var result = await AnalyzerFixture.AnalyzeAsync(project, new PythonProjectAnalyzer());

        PythonModuleAnalysisData module = Assert.Single(result.Scopes
            .SelectMany(scope => scope.AnalysisData ?? [])
            .OfType<PythonModuleAnalysisData>());
        Assert.Equal("app", module.ModuleName);
    }
}
