using AllCodeChecker.Diagnostics;
using AllCodeChecker.Results;
using AllCodeChecker.Rules;

namespace AllCodeChecker.Core.Tests;

public sealed class DiagnosticFoundationTests
{
    [Theory]
    [InlineData("ACI001", RuleCategory.FundamentalCorrectness)]
    [InlineData("ACI100", RuleCategory.RuntimeBehavior)]
    [InlineData("ACI200", RuleCategory.Maintainability)]
    [InlineData("ACI300", RuleCategory.TestAndCoverage)]
    [InlineData("ACI400", RuleCategory.ConfigurationAndProjectMetadata)]
    [InlineData("ACI500", RuleCategory.Security)]
    [InlineData("ACI600", RuleCategory.DataBoundary)]
    [InlineData("ACI700", RuleCategory.DependencyAndEnvironment)]
    [InlineData("ACI800", RuleCategory.LanguageSpecific)]
    [InlineData("ACI900", RuleCategory.Internal)]
    [InlineData("ACI999", RuleCategory.Internal)]
    public void RuleIdMapsToExpectedCategory(string text, RuleCategory expected)
    {
        AciRuleId id = AciRuleId.Parse(text);

        Assert.Equal(expected, id.Category);
        Assert.Equal(text, id.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ACI000")]
    [InlineData("ACI1000")]
    [InlineData("aci001")]
    [InlineData("ACI01")]
    public void InvalidRuleIdIsRejected(string? text)
    {
        Assert.False(AciRuleId.TryParse(text, out _));
    }

    [Fact]
    public void PublishedRuleCatalogReservesDocumentedIds()
    {
        RuleRegistry registry = PublishedRuleCatalog.CreateRegistry();

        Assert.True(registry.TryGet(AciRuleId.Parse("ACI001"), out RuleDefinition? definition));
        Assert.Equal("解析失敗", definition!.Name);
        Assert.Throws<InvalidOperationException>(
            () => registry.Register(new RuleDefinition(AciRuleId.Parse("ACI001"), "再利用")));
    }

    [Fact]
    public void DuplicateRuleRegistrationIsRejected()
    {
        var registry = new RuleRegistry();
        var id = AciRuleId.Parse("ACI001");

        registry.Register(new RuleDefinition(id, "解析失敗"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => registry.Register(new RuleDefinition(id, "別のルール")));

        Assert.Contains("ACI001", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OverallStatusUsesStrongestEnabledSeverity()
    {
        CheckerDiagnostic[] diagnostics =
        [
            new("ACI201", DiagnosticSeverity.Notice, "notice"),
            new("ACI101", DiagnosticSeverity.Warning, "warning"),
            new("ACI001", DiagnosticSeverity.Danger, "danger")
        ];

        DiagnosticSeverity? result = OverallStatus.Calculate(
            diagnostics,
            new SeveritySelection(Danger: false, Warning: true, Notice: true));

        Assert.Equal(DiagnosticSeverity.Warning, result);
    }

    [Fact]
    public void OverallStatusIsNormalWhenAllDiagnosticsAreFilteredOut()
    {
        CheckerDiagnostic[] diagnostics =
        [
            new("ACI201", DiagnosticSeverity.Notice, "notice")
        ];

        DiagnosticSeverity? result = OverallStatus.Calculate(
            diagnostics,
            new SeveritySelection(Danger: true, Warning: true, Notice: false));

        Assert.Null(result);
    }

    [Fact]
    public void DiagnosticCarriesLocationAndLanguage()
    {
        var diagnostic = new CheckerDiagnostic(
            "ACI003",
            DiagnosticSeverity.Danger,
            "未解決シンボルです。",
            "src/example.ts",
            12,
            8,
            "typescript");

        Assert.Equal("src/example.ts", diagnostic.Path);
        Assert.Equal(12, diagnostic.Line);
        Assert.Equal(8, diagnostic.Column);
        Assert.Equal("typescript", diagnostic.Language);
    }
}
