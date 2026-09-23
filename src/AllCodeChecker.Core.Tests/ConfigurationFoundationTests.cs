using AllCodeChecker.Configuration;
using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;
using AllCodeChecker.Results;
using Xunit;

namespace AllCodeChecker.Core.Tests;

public sealed class ConfigurationFoundationTests
{
    [Fact]
    public void MissingConfigurationUsesAllEnabledDefaults()
    {
        using var fixture = new ConfigurationFixture();
        string target = fixture.CreateFile("src/main.cs", "class MainType {}");

        var loader = new CheckerConfigurationLoader();
        ConfigurationLoadResult result = loader.Load(target);

        Assert.True(result.IsDefault);
        Assert.True(result.Configuration.Severities.Danger);
        Assert.True(result.Configuration.Severities.Warning);
        Assert.True(result.Configuration.Severities.Notice);
        Assert.Empty(result.Configuration.IgnorePaths);
        Assert.Empty(result.Configuration.Rules);
    }

    [Fact]
    public void NearestConfigurationWins()
    {
        using var fixture = new ConfigurationFixture();
        fixture.CreateFile(
            "all-code-checker.json",
            """{"severity":{"warning":false}}""");
        string nestedConfiguration = fixture.CreateFile(
            "apps/tool/all-code-checker.json",
            """{"severity":{"notice":false}}""");
        string target = fixture.CreateFile("apps/tool/src/main.cs", "class MainType {}");

        var loader = new CheckerConfigurationLoader();
        ConfigurationLoadResult result = loader.Load(target);

        Assert.Equal(nestedConfiguration, result.ConfigurationPath);
        Assert.True(result.Configuration.Severities.Danger);
        Assert.True(result.Configuration.Severities.Warning);
        Assert.False(result.Configuration.Severities.Notice);
    }

    [Fact]
    public void ExplicitMissingConfigurationDoesNotFallBack()
    {
        using var fixture = new ConfigurationFixture();
        fixture.CreateFile("all-code-checker.json", "{}");
        string target = fixture.CreateFile("src/main.cs", "class MainType {}");
        string missing = Path.Combine(fixture.Root, "missing.json");

        var loader = new CheckerConfigurationLoader();

        Assert.Throws<ConfigurationException>(() => loader.Load(target, missing));
    }

    [Fact]
    public void InvalidConfigurationDoesNotBecomeSuccessfulDefault()
    {
        using var fixture = new ConfigurationFixture();
        fixture.CreateFile("all-code-checker.json", """{"severity":{"danger":"yes"}}""");
        string target = fixture.CreateFile("src/main.cs", "class MainType {}");

        var loader = new CheckerConfigurationLoader();

        Assert.Throws<ConfigurationException>(() => loader.Load(target));
    }

    [Fact]
    public void UnknownTopLevelPropertyIsRejected()
    {
        using var fixture = new ConfigurationFixture();
        fixture.CreateFile("all-code-checker.json", """{"severty":{"danger":false}}""");
        string target = fixture.CreateFile("src/main.cs", "class MainType {}");

        var loader = new CheckerConfigurationLoader();

        Assert.Throws<ConfigurationException>(() => loader.Load(target));
    }

    [Fact]
    public void RuleEnablementAndFutureSettingsArePreserved()
    {
        using var fixture = new ConfigurationFixture();
        fixture.CreateFile(
            "all-code-checker.json",
            """
            {
              "rules": {
                "ACI201": {
                  "enabled": false,
                  "settings": {
                    "maxLines": 120,
                    "mode": "advisory"
                  }
                }
              }
            }
            """);
        string target = fixture.CreateFile("src/main.cs", "class MainType {}");

        var loader = new CheckerConfigurationLoader();
        CheckerConfiguration configuration = loader.Load(target).Configuration;

        RuleConfiguration rule = configuration.Rules["ACI201"];
        Assert.False(rule.Enabled);
        Assert.Equal(120, rule.Settings["maxLines"].GetInt32());
        Assert.Equal("advisory", rule.Settings["mode"].GetString());
    }

    [Fact]
    public void SeverityFilterDoesNotMutateAnalyzerDiagnostics()
    {
        CheckerDiagnostic[] diagnostics =
        [
            new("ACI001", DiagnosticSeverity.Danger, "danger"),
            new("ACI101", DiagnosticSeverity.Warning, "warning"),
            new("ACI201", DiagnosticSeverity.Notice, "notice")
        ];
        var enabled = new SeveritySelection(Danger: true, Warning: false, Notice: false);

        IReadOnlyList<CheckerDiagnostic> filtered = DiagnosticFilter.Apply(diagnostics, enabled);

        Assert.Single(filtered);
        Assert.Equal(DiagnosticSeverity.Danger, filtered[0].Severity);
        Assert.Equal(3, diagnostics.Length);
        Assert.Equal(DiagnosticSeverity.Danger, OverallStatus.Calculate(diagnostics, enabled));
    }

    [Fact]
    public void ConfiguredIgnorePathIsAppliedToDiscovery()
    {
        using var fixture = new ConfigurationFixture();
        fixture.CreateFile(
            "all-code-checker.json",
            """{"ignorePaths":["examples/**","**/*.generated.cs"]}""");
        string included = fixture.CreateFile("src/main.cs", "class MainType {}");
        fixture.CreateFile("examples/sample.cs", "class Sample {}");
        fixture.CreateFile("src/model.generated.cs", "class Generated {}");

        var loader = new CheckerConfigurationLoader();
        CheckerConfiguration configuration = loader.Load(fixture.Root).Configuration;
        var discovery = new AnalysisInputDiscovery(new DiscoveryPathPolicy(configuration.IgnorePaths));

        AnalysisDiscoveryResult result = discovery.Discover(fixture.Root);

        DiscoveredSourceGroup group = Assert.Single(result.Groups);
        Assert.Equal(new[] { included }, group.SourceFiles);
    }

    [Fact]
    public void DefaultGeneratedAndVendorDirectoriesRemainExcluded()
    {
        using var fixture = new ConfigurationFixture();
        string included = fixture.CreateFile("src/main.py", "value = 1");
        fixture.CreateFile("generated/output.py", "value = 2");
        fixture.CreateFile("vendor/library.py", "value = 3");

        var discovery = new AnalysisInputDiscovery(new DiscoveryPathPolicy());

        AnalysisDiscoveryResult result = discovery.Discover(fixture.Root);

        DiscoveredSourceGroup group = Assert.Single(result.Groups);
        Assert.Equal(new[] { included }, group.SourceFiles);
    }

    private sealed class ConfigurationFixture : IDisposable
    {
        public ConfigurationFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"all-code-checker-config-{Guid.NewGuid():N}");
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
