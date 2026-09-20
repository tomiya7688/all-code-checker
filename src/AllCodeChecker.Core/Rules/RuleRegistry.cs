namespace AllCodeChecker.Rules;

public sealed class RuleRegistry
{
    private readonly Dictionary<AciRuleId, RuleDefinition> definitions = [];

    public IReadOnlyCollection<RuleDefinition> Definitions => definitions.Values;

    public void Register(RuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Rule名は空にできません。", nameof(definition));
        }

        if (!definitions.TryAdd(definition.Id, definition))
        {
            throw new InvalidOperationException($"Rule ID {definition.Id} は既に登録されています。");
        }
    }

    public bool TryGet(AciRuleId id, out RuleDefinition? definition) =>
        definitions.TryGetValue(id, out definition);
}
