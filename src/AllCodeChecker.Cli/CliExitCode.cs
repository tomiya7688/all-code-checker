namespace AllCodeChecker.Cli;

internal enum CliExitCode
{
    Success = 0,
    DiagnosticsFound = 1,
    InputError = 2,
    AnalysisFailure = 3
}
