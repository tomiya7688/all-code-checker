using System.Text.Json;

namespace AllCodeChecker.Configuration;

public sealed record RuleConfiguration(
    bool Enabled,
    IReadOnlyDictionary<string, JsonElement> Settings);
