namespace AllCodeChecker.Cli;

internal static class CliParser
{
    public static CliOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        CliOptions options = CliOptions.Default;
        string? target = null;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];

            switch (argument)
            {
                case "--help":
                case "-h":
                    options = options with { ShowHelp = true };
                    break;
                case "--version":
                case "-V":
                    options = options with { ShowVersion = true };
                    break;
                case "--config":
                    options = options with
                    {
                        ConfigurationPath = RequireValue(args, ref index, argument)
                    };
                    break;
                case "--no-danger":
                    options = options with { ShowDanger = false };
                    break;
                case "--no-warning":
                    options = options with { ShowWarning = false };
                    break;
                case "--no-notice":
                    options = options with { ShowNotice = false };
                    break;
                case "--coverage":
                    options = options with { CoverageRequested = true };
                    break;
                case "--fail-on":
                    options = options with
                    {
                        FailureThreshold = ParseFailureThreshold(
                            RequireValue(args, ref index, argument))
                    };
                    break;
                case "--format":
                    options = options with
                    {
                        OutputFormat = ParseOutputFormat(
                            RequireValue(args, ref index, argument))
                    };
                    break;
                case "--output":
                    options = options with
                    {
                        OutputPath = RequireValue(args, ref index, argument)
                    };
                    break;
                default:
                    if (argument.StartsWith('-'))
                    {
                        throw new CliUsageException($"不明なoptionです: {argument}");
                    }

                    if (target is not null)
                    {
                        throw new CliUsageException("解析対象は1つだけ指定できます。");
                    }

                    target = argument;
                    break;
            }
        }

        options = options with { TargetPath = target };

        if (!options.ShowHelp && !options.ShowVersion && string.IsNullOrWhiteSpace(options.TargetPath))
        {
            throw new CliUsageException("解析対象を指定してください。");
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        int valueIndex = index + 1;

        if (valueIndex >= args.Length || args[valueIndex].StartsWith('-'))
        {
            throw new CliUsageException($"{option} には値が必要です。");
        }

        index = valueIndex;
        return args[valueIndex];
    }

    private static CliFailureThreshold ParseFailureThreshold(string value) =>
        value.ToLowerInvariant() switch
        {
            "danger" => CliFailureThreshold.Danger,
            "warning" => CliFailureThreshold.Warning,
            "notice" => CliFailureThreshold.Notice,
            "never" => CliFailureThreshold.Never,
            _ => throw new CliUsageException(
                "--fail-on は danger|warning|notice|never のいずれかを指定してください。")
        };

    private static CliOutputFormat ParseOutputFormat(string value) =>
        value.ToLowerInvariant() switch
        {
            "text" => CliOutputFormat.Text,
            "json" => CliOutputFormat.Json,
            "sarif" => CliOutputFormat.Sarif,
            "github" => CliOutputFormat.GitHub,
            _ => throw new CliUsageException(
                "--format は text|json|sarif|github のいずれかを指定してください。")
        };
}
