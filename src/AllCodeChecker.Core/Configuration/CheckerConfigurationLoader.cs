using System.Text.Json;
using System.Text.Json.Serialization;
using AllCodeChecker.Results;
using AllCodeChecker.Rules;

namespace AllCodeChecker.Configuration;

public sealed class CheckerConfigurationLoader
{
    public const string DefaultFileName = "all-code-checker.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public ConfigurationLoadResult Load(string targetPath, string? explicitConfigurationPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        if (!string.IsNullOrWhiteSpace(explicitConfigurationPath))
        {
            string explicitPath = Path.GetFullPath(explicitConfigurationPath);

            if (!File.Exists(explicitPath))
            {
                throw new ConfigurationException($"明示指定された設定ファイルが見つかりません: {explicitPath}");
            }

            return new ConfigurationLoadResult(
                LoadFile(explicitPath),
                explicitPath);
        }

        string? discoveredPath = FindNearestConfiguration(targetPath);

        return discoveredPath is null
            ? new ConfigurationLoadResult(CheckerConfiguration.Default, null)
            : new ConfigurationLoadResult(LoadFile(discoveredPath), discoveredPath);
    }

    public string? FindNearestConfiguration(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        string fullTarget = Path.GetFullPath(targetPath);
        string? current = Directory.Exists(fullTarget)
            ? fullTarget
            : Path.GetDirectoryName(fullTarget);

        while (current is not null)
        {
            string candidate = Path.Combine(current, DefaultFileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static CheckerConfiguration LoadFile(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            ConfigurationDocument? document = JsonSerializer.Deserialize<ConfigurationDocument>(json, JsonOptions);

            if (document is null)
            {
                throw new ConfigurationException($"設定ファイルが空です: {path}");
            }

            return Normalize(document, path);
        }
        catch (ConfigurationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException($"設定ファイルのJSONが不正です: {path}", exception);
        }
        catch (IOException exception)
        {
            throw new ConfigurationException($"設定ファイルを読み込めません: {path}", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ConfigurationException($"設定ファイルを読み込めません: {path}", exception);
        }
    }

    private static CheckerConfiguration Normalize(ConfigurationDocument document, string path)
    {
        SeverityDocument severity = document.Severity ?? new SeverityDocument();
        var selection = new SeveritySelection(
            Danger: severity.Danger ?? true,
            Warning: severity.Warning ?? true,
            Notice: severity.Notice ?? true);

        string[] ignorePaths = (document.IgnorePaths ?? [])
            .Select(pattern => ValidateIgnorePattern(pattern, path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var rules = new Dictionary<string, RuleConfiguration>(StringComparer.OrdinalIgnoreCase);

        foreach ((string rawRuleId, RuleDocument rawRule) in document.Rules
                     ?? new Dictionary<string, RuleDocument>(StringComparer.OrdinalIgnoreCase))
        {
            if (!AciRuleId.TryParse(rawRuleId, out AciRuleId ruleId))
            {
                throw new ConfigurationException(
                    $"設定ファイルに不正なrule IDがあります: {rawRuleId} ({path})");
            }

            string normalizedRuleId = ruleId.ToString();

            if (rules.ContainsKey(normalizedRuleId))
            {
                throw new ConfigurationException(
                    $"設定ファイルに重複するrule IDがあります: {normalizedRuleId} ({path})");
            }

            IReadOnlyDictionary<string, JsonElement> settings = rawRule.Settings
                ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            rules.Add(
                normalizedRuleId,
                new RuleConfiguration(rawRule.Enabled ?? true, settings));
        }

        return new CheckerConfiguration(selection, ignorePaths, rules);
    }

    private static string ValidateIgnorePattern(string? pattern, string path)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ConfigurationException($"ignorePathsに空のpatternは指定できません: {path}");
        }

        return pattern.Replace('\\', '/').Trim();
    }

    private sealed class ConfigurationDocument
    {
        public SeverityDocument? Severity { get; init; }

        public List<string>? IgnorePaths { get; init; }

        public Dictionary<string, RuleDocument>? Rules { get; init; }
    }

    private sealed class SeverityDocument
    {
        public bool? Danger { get; init; }

        public bool? Warning { get; init; }

        public bool? Notice { get; init; }
    }

    private sealed class RuleDocument
    {
        public bool? Enabled { get; init; }

        public Dictionary<string, JsonElement>? Settings { get; init; }
    }
}
