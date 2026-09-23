using AllCodeChecker.Results;

namespace AllCodeChecker.Configuration;

public sealed record CheckerConfiguration(
    SeveritySelection Severities,
    IReadOnlyList<string> IgnorePaths,
    IReadOnlyDictionary<string, RuleConfiguration> Rules)
{
    public static CheckerConfiguration Default { get; } = new(
        SeveritySelection.All,
        [],
        new Dictionary<string, RuleConfiguration>(StringComparer.OrdinalIgnoreCase));
}
