using AllCodeChecker.Analysis;
using AllCodeChecker.Discovery;
using Xunit;

namespace AllCodeChecker.Core.Tests;

public sealed class CSharpProjectAnalyzerTests
{
    [Fact]
    public async Task SyntaxErrorsUseSyntaxRuleWithoutDuplicateCompilerFailure()
    {
        using var fixture = new AnalyzerFixture();
        string file = fixture.Write("Broken.cs", "class Broken { int M() { return 1 } }");

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(file, new CSharpProjectAnalyzer());

        Assert.Single(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI002");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI001"
            && diagnostic.Reason.Contains("CS1002", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("class C { object M() => missing; }", "ACI003")]
    [InlineData("class C { int M() => \"wrong\"; }", "ACI004")]
    [InlineData("class C { object M() => new C().Missing; }", "ACI005")]
    [InlineData("class C { void M() { N(); } void N(int x) {} }", "ACI006")]
    [InlineData("#nullable enable\nclass C { string M(string? value) => value; }", "ACI008")]
    public async Task SemanticDiagnosticsUsePublishedRules(string source, string rule)
    {
        using var fixture = new AnalyzerFixture();
        string file = fixture.Write("Input.cs", source);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(file, new CSharpProjectAnalyzer());

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == rule);
    }

    [Fact]
    public async Task LocalFunctionsAndLambdasHaveTheirOwnFlowGraphs()
    {
        using var fixture = new AnalyzerFixture();
        string file = fixture.Write("Flows.cs", """
            class C
            {
                int M(int x)
                {
                    int Local(int y) => y + x;
                    System.Func<int, int> f = y => Local(y);
                    return f(x);
                }
            }
            """);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(file, new CSharpProjectAnalyzer());

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Reason.Contains("flow解析", StringComparison.Ordinal));
        CSharpFlowAnalysisSummary[] summaries = Assert.Single(result.Scopes).AnalysisData!
            .OfType<CSharpFlowAnalysisSummary>().ToArray();
        Assert.Equal(3, summaries.Length);
        Assert.All(summaries, summary => Assert.NotEmpty(summary.Blocks));
        CSharpFlowAnalysisSummary local = Assert.Single(summaries,
            summary => summary.Symbol.Contains("Local", StringComparison.Ordinal));
        Assert.Contains("int y", local.DataFlowsIn);
        Assert.Contains("int x", local.ReadInside);
        CSharpFlowAnalysisSummary method = Assert.Single(summaries,
            summary => summary.Symbol == "C.M(int)");
        Assert.Contains("int x", method.Captured);
    }

    [Fact]
    public async Task GeneratedDeclarationsRemainAvailableForSemanticBinding()
    {
        using var fixture = new AnalyzerFixture();
        string project = fixture.Project("App.csproj");
        fixture.Write("App.cs", "class App { int M() => Generated.Value; }");
        fixture.Write("Generated.g.cs", "class Generated { public static int Value => 42; }");
        await AnalyzerFixture.RestoreAsync(project);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(project, new CSharpProjectAnalyzer());

        Assert.True(result.IsComplete, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(Assert.Single(result.Scopes).AnalysisData!.OfType<CSharpFlowAnalysisSummary>(),
            summary => summary.FilePath.EndsWith(".g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DirectProjectDoesNotReportReferencedProjectDiagnostics()
    {
        using var fixture = new AnalyzerFixture();
        string project = fixture.Project("App/App.csproj", """
            <ItemGroup><ProjectReference Include="../Library/Library.csproj" /></ItemGroup>
            """);
        fixture.Project("Library/Library.csproj");
        string dependencyFile = fixture.Write("Library/Library.cs", "public class Library { void M() { Missing(); } }");
        fixture.Write("App/App.cs", "class App { Library M() => new Library(); }");
        await AnalyzerFixture.RestoreAsync(project);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(project, new CSharpProjectAnalyzer());

        Assert.True(result.IsComplete, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Path == dependencyFile);
        Assert.DoesNotContain(Assert.Single(result.Scopes).AnalysisData!.OfType<CSharpFlowAnalysisSummary>(),
            summary => summary.FilePath == dependencyFile);
    }

    [Fact]
    public async Task SolutionKeepsUnrelatedCompilationsSeparate()
    {
        using var fixture = new AnalyzerFixture();
        fixture.Project("One/One.csproj");
        fixture.Project("Two/Two.csproj");
        string one = fixture.Write("One/Input.cs", "class Duplicate { int M() => 1; }");
        string two = fixture.Write("Two/Input.cs", "class Duplicate { int M() => 2; }");
        string solution = fixture.Write("Apps.sln", """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "One", "One/One.csproj", "{70FC2888-EE21-4000-B6FD-8E3C52EA8749}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Two", "Two/Two.csproj", "{B7070FD9-C302-4DAD-B08D-631D449B187A}"
            EndProject
            Global
                GlobalSection(SolutionConfigurationPlatforms) = preSolution
                    Debug|Any CPU = Debug|Any CPU
                EndGlobalSection
                GlobalSection(ProjectConfigurationPlatforms) = postSolution
                    {70FC2888-EE21-4000-B6FD-8E3C52EA8749}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {70FC2888-EE21-4000-B6FD-8E3C52EA8749}.Debug|Any CPU.Build.0 = Debug|Any CPU
                    {B7070FD9-C302-4DAD-B08D-631D449B187A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {B7070FD9-C302-4DAD-B08D-631D449B187A}.Debug|Any CPU.Build.0 = Debug|Any CPU
                EndGlobalSection
            EndGlobal
            """);
        await AnalyzerFixture.RestoreAsync(solution);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(solution, new CSharpProjectAnalyzer());

        Assert.True(result.IsComplete, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        CSharpFlowAnalysisSummary[] flows = Assert.Single(result.Scopes).AnalysisData!
            .OfType<CSharpFlowAnalysisSummary>().ToArray();
        Assert.Contains(flows, flow => flow.FilePath == one);
        Assert.Contains(flows, flow => flow.FilePath == two);
    }

    [Fact]
    public async Task MissingProjectReferenceProducesPartialWorkspaceDiagnostic()
    {
        using var fixture = new AnalyzerFixture();
        string project = fixture.Project("Broken.csproj", """
            <ItemGroup><ProjectReference Include="missing/Missing.csproj" /></ItemGroup>
            """);
        fixture.Write("App.cs", "class App {}");
        await AnalyzerFixture.RestoreAsync(project);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(project, new CSharpProjectAnalyzer());

        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI001"
            && diagnostic.Reason.Contains("workspace", StringComparison.OrdinalIgnoreCase)
            && diagnostic.Reason.Contains("Missing.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SingleSourceUsesProjectContextButReportsOnlyEntryFile()
    {
        using var fixture = new AnalyzerFixture();
        string project = fixture.Project("App.csproj");
        string entry = fixture.Write("App.cs", "class App { int M() => Helper.Value; }");
        fixture.Write("Helper.cs", "class Helper { public static int Value => Missing; }");
        await AnalyzerFixture.RestoreAsync(project);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(entry, new CSharpProjectAnalyzer());

        Assert.True(result.IsComplete, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        Assert.All(Assert.Single(result.Scopes).AnalysisData!.OfType<CSharpFlowAnalysisSummary>(),
            summary => Assert.Equal(entry, summary.FilePath));
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        var request = new AnalyzerRequest(new DiscoveredSourceGroup(SupportedLanguage.CSharp, ".", []));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CSharpProjectAnalyzer().AnalyzeAsync(request, cancellation.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectoryKeepsAllProjectsAndAvailableResultsAtSameBoundary(bool missingReference)
    {
        using var fixture = new AnalyzerFixture();
        fixture.Write("Directory.Build.props", """
            <Project><PropertyGroup>
              <BaseIntermediateOutputPath>obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>
              <MSBuildProjectExtensionsPath>$(BaseIntermediateOutputPath)</MSBuildProjectExtensionsPath>
            </PropertyGroup></Project>
            """);
        string extraReference = missingReference
            ? "<ItemGroup><ProjectReference Include=\"missing/Missing.csproj\" /></ItemGroup>" : "";
        string one = fixture.Project("One.csproj", """
            <PropertyGroup><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
            <ItemGroup><Compile Include="One.cs" /></ItemGroup>
            """ + extraReference);
        string two = fixture.Project("Two.csproj", """
            <PropertyGroup><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
            <ItemGroup><Compile Include="Two.cs" /></ItemGroup>
            """);
        string oneFile = fixture.Write("One.cs", "class Duplicate { int M() => MissingOne; }");
        string twoFile = fixture.Write("Two.cs", "class Duplicate { int M() => MissingTwo; }");
        await AnalyzerFixture.RestoreAsync(one);
        await AnalyzerFixture.RestoreAsync(two);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(fixture.Root, new CSharpProjectAnalyzer());

        Assert.Equal(!missingReference, result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI003" && diagnostic.Path == oneFile);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI003" && diagnostic.Path == twoFile);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Reason.Contains("CS0101", StringComparison.Ordinal));
        CSharpFlowAnalysisSummary[] flows = Assert.Single(result.Scopes).AnalysisData!
            .OfType<CSharpFlowAnalysisSummary>().ToArray();
        Assert.Contains(flows, flow => flow.FilePath == oneFile);
        Assert.Contains(flows, flow => flow.FilePath == twoFile);
        if (missingReference)
        {
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI001"
                && diagnostic.Reason.Contains("Missing.csproj", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SourceExcludedFromCompileItemsIsReportedAsPartial()
    {
        using var fixture = new AnalyzerFixture();
        string project = fixture.Project("App.csproj", """
            <ItemGroup><Compile Remove="Excluded.cs" /></ItemGroup>
            """);
        fixture.Write("Included.cs", "class Included {}");
        string excluded = fixture.Write("Excluded.cs", "class Excluded { int M() => Missing; }");
        await AnalyzerFixture.RestoreAsync(project);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(excluded, new CSharpProjectAnalyzer());

        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI903");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ACI001"
            && diagnostic.Reason.Contains("Compile item", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StandaloneTopLevelStatementsHaveEntryPointFlowWithoutSpuriousCompilerErrors()
    {
        using var fixture = new AnalyzerFixture();
        string entry = fixture.Write("TopLevel.cs", "int value = 1; System.Console.WriteLine(value);");

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(entry, new CSharpProjectAnalyzer());

        Assert.False(result.IsComplete);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("ACI903", diagnostic.RuleId));
        CSharpFlowAnalysisSummary flow = Assert.Single(Assert.Single(result.Scopes).AnalysisData!
            .OfType<CSharpFlowAnalysisSummary>());
        Assert.NotEmpty(flow.Blocks);
        Assert.EndsWith("value", Assert.Single(flow.VariablesDeclared), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("class C { string M() => \"<auto-generated\"; }")]
    [InlineData("class C { int M() { /* <auto-generated> */ return 1; } }")]
    public async Task GeneratedMarkerInUserCodeDoesNotSkipAnalysis(string source)
    {
        using var fixture = new AnalyzerFixture();
        string entry = fixture.Write("UserCode.cs", source);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(entry, new CSharpProjectAnalyzer());

        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("ACI903", diagnostic.RuleId));
        Assert.Single(Assert.Single(result.Scopes).AnalysisData!.OfType<CSharpFlowAnalysisSummary>());
    }

    [Theory]
    [InlineData("// <auto-generated/>\n")]
    [InlineData("/* <auto-generated> */\n")]
    public async Task GeneratedHeaderExcludesDiagnosticsAndFlow(string header)
    {
        using var fixture = new AnalyzerFixture();
        string entry = fixture.Write("Generated.cs", header + "class Generated { int M() => Missing; }");

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(entry, new CSharpProjectAnalyzer());

        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("ACI903", diagnostic.RuleId));
        Assert.Empty(Assert.Single(result.Scopes).AnalysisData!);
    }
}
