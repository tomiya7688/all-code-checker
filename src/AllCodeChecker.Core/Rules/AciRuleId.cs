using System.Globalization;

namespace AllCodeChecker.Rules;

public readonly record struct AciRuleId
{
    private AciRuleId(int number)
    {
        Number = number;
    }

    public int Number { get; }

    public RuleCategory Category => GetCategory(Number);

    public override string ToString() => $"ACI{Number.ToString("D3", CultureInfo.InvariantCulture)}";

    public static AciRuleId Parse(string value)
    {
        if (!TryParse(value, out AciRuleId result))
        {
            throw new FormatException("ACI rule IDはACI001からACI999の形式で指定してください。");
        }

        return result;
    }

    public static bool TryParse(string? value, out AciRuleId result)
    {
        result = default;

        if (value is null ||
            value.Length != 6 ||
            !value.StartsWith("ACI", StringComparison.Ordinal) ||
            !int.TryParse(value.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
            number is < 1 or > 999)
        {
            return false;
        }

        result = new AciRuleId(number);
        return true;
    }

    public static RuleCategory GetCategory(int number) =>
        number switch
        {
            >= 1 and <= 99 => RuleCategory.FundamentalCorrectness,
            >= 100 and <= 199 => RuleCategory.RuntimeBehavior,
            >= 200 and <= 299 => RuleCategory.Maintainability,
            >= 300 and <= 399 => RuleCategory.TestAndCoverage,
            >= 400 and <= 499 => RuleCategory.ConfigurationAndProjectMetadata,
            >= 500 and <= 599 => RuleCategory.Security,
            >= 600 and <= 699 => RuleCategory.DataBoundary,
            >= 700 and <= 799 => RuleCategory.DependencyAndEnvironment,
            >= 800 and <= 899 => RuleCategory.LanguageSpecific,
            >= 900 and <= 999 => RuleCategory.Internal,
            _ => throw new ArgumentOutOfRangeException(nameof(number), number, "ACI rule numberは1から999の範囲で指定してください。")
        };
}
