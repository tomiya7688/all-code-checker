namespace AllCodeChecker.Cli;

internal sealed record CliOptions(
    string? TargetPath,
    string? ConfigurationPath,
    bool ShowDanger,
    bool ShowWarning,
    bool ShowNotice,
    bool CoverageRequested,
    CliFailureThreshold FailureThreshold,
    CliOutputFormat OutputFormat,
    string? OutputPath,
    bool ShowHelp,
    bool ShowVersion)
{
    public static CliOptions Default { get; } = new(
        TargetPath: null,
        ConfigurationPath: null,
        ShowDanger: true,
        ShowWarning: true,
        ShowNotice: true,
        CoverageRequested: false,
        FailureThreshold: CliFailureThreshold.Danger,
        OutputFormat: CliOutputFormat.Text,
        OutputPath: null,
        ShowHelp: false,
        ShowVersion: false);
}
