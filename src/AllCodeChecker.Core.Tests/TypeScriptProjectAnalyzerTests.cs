using AllCodeChecker.Analysis;
using AllCodeChecker.Discovery;
using Xunit;

namespace AllCodeChecker.Core.Tests;

public sealed class TypeScriptProjectAnalyzerTests
{
    [Fact]
    public async Task ProjectAnalysisUsesCompilerTypesAndReportsSemanticErrors()
    {
        using var fixture = new AnalyzerFixture();
        string project = fixture.Write("tsconfig.json", """
            {"compilerOptions":{"strict":true,"noEmit":true},"include":["src/**/*.ts"]}
            """);
        fixture.Write("package.json", """
            {"scripts":{"test":"vitest run","lint":"eslint .","spec":"playwright test"}}
            """);
        string source = fixture.Write("src/model.ts", """
            interface Model { id: string }
            const payload: any = {};
            const unknownValue: unknown = payload;
            const maybe: string | null = null;
            const dto: Model = { id: "x" };
            const invalid: number = "x";
            """);

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(
            project, new TypeScriptProjectAnalyzer());

        AnalyzerScopeResult scope = Assert.Single(result.Scopes);
        Assert.True(scope.Status == AnalyzerCompletionStatus.Complete,
            string.Join(Environment.NewLine, scope.Diagnostics.Select(item => item.Reason)));
        Assert.Contains(scope.Diagnostics, diagnostic => diagnostic.RuleId == "ACI001"
            && diagnostic.Path == source
            && diagnostic.Reason.Contains("TS2322", StringComparison.Ordinal));
        Assert.DoesNotContain(scope.Diagnostics, diagnostic => diagnostic.RuleId == "ACI902");

        TypeScriptProjectAnalysisData data = Assert.Single(scope.AnalysisData!
            .OfType<TypeScriptProjectAnalysisData>());
        Assert.True(data.StrictNullChecks);
        Assert.Contains("test", data.TestScripts);
        Assert.Contains("spec", data.TestScripts);
        Assert.DoesNotContain("lint", data.TestScripts);
        Assert.All(data.Symbols, symbol => Assert.Equal(source, symbol.FilePath));
        Assert.Contains(data.Symbols, symbol => symbol.Name == "payload" && symbol.IsAny);
        Assert.Contains(data.Symbols, symbol => symbol.Name == "unknownValue" && symbol.IsUnknown);
        Assert.Contains(data.Symbols, symbol => symbol.Name == "maybe" && symbol.IncludesNull);
        Assert.Contains(data.Symbols, symbol => symbol.Name == "dto"
            && symbol.Properties.Contains("id"));
    }

    [Fact]
    public async Task NestedTypeScriptProjectKeepsSyntaxErrorInsideItsBoundary()
    {
        using var fixture = new AnalyzerFixture();
        fixture.Write("tsconfig.json", """
            {"compilerOptions":{"strict":true},"include":["src/**/*.ts"]}
            """);
        fixture.Write("src/good.ts", "export const good: number = 1;\n");
        fixture.Write("src/nested/tsconfig.json", """
            {"compilerOptions":{"strict":true},"include":["*.ts"]}
            """);
        string broken = fixture.Write("src/nested/broken.ts", "const broken: = ;\n");

        AnalysisOrchestrationResult result = await AnalyzerFixture.AnalyzeAsync(
            fixture.Root, new TypeScriptProjectAnalyzer());

        Assert.Equal(2, result.Scopes.Count);
        AnalyzerScopeResult root = Assert.Single(result.Scopes,
            scope => scope.BoundaryPath == fixture.Root);
        AnalyzerScopeResult nested = Assert.Single(result.Scopes,
            scope => scope.BoundaryPath == Path.GetDirectoryName(broken));
        Assert.True(root.Status == AnalyzerCompletionStatus.Complete,
            string.Join(Environment.NewLine, root.Diagnostics.Select(item => item.Reason)));
        Assert.DoesNotContain(root.Diagnostics, diagnostic => diagnostic.Path == broken);
        Assert.Equal(AnalyzerCompletionStatus.Partial, nested.Status);
        Assert.Contains(nested.Diagnostics, diagnostic => diagnostic.RuleId == "ACI002"
            && diagnostic.Path == broken);
        Assert.Contains(nested.Diagnostics, diagnostic => diagnostic.RuleId == "ACI903"
            && diagnostic.Reason.Contains("TypeScript projectの一部", StringComparison.Ordinal));
    }
}
