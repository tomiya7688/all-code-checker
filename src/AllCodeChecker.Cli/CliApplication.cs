using System.Reflection;
using AllCodeChecker.Configuration;
using AllCodeChecker.Diagnostics;
using AllCodeChecker.Discovery;
using AllCodeChecker.Results;

namespace AllCodeChecker.Cli;

internal static class CliApplication
{
    public static int Run(
        string[] args,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        try
        {
            CliOptions options = CliParser.Parse(args);

            if (options.ShowHelp)
            {
                standardOutput.WriteLine(HelpText);
                return (int)CliExitCode.Success;
            }

            if (options.ShowVersion)
            {
                standardOutput.WriteLine(ProductVersion);
                return (int)CliExitCode.Success;
            }

            if (options.CoverageRequested)
            {
                throw new CliUsageException(
                    "--coverage はcoverage実装が追加されるまで利用できません。");
            }

            string targetPath = Path.GetFullPath(options.TargetPath!);
            ConfigurationLoadResult configurationResult =
                CheckerConfigurationLoader.Load(
                    targetPath,
                    options.ConfigurationPath);

            CheckerConfiguration configuration = configurationResult.Configuration;
            var enabledSeverities = new SeveritySelection(
                Danger: configuration.Severities.Danger && options.ShowDanger,
                Warning: configuration.Severities.Warning && options.ShowWarning,
                Notice: configuration.Severities.Notice && options.ShowNotice);

            var discovery = new AnalysisInputDiscovery(
                new DiscoveryPathPolicy(configuration.IgnorePaths));

            AnalysisDiscoveryResult discoveryResult = discovery.Discover(targetPath);

            if (discoveryResult.Groups.Count == 0)
            {
                throw new UnsupportedInputException(
                    "対応しているsource fileが解析対象内に見つかりません。");
            }

            IReadOnlyList<CheckerDiagnostic> analyzerDiagnostics = [];
            IReadOnlyList<CheckerDiagnostic> filteredDiagnostics =
                DiagnosticFilter.Apply(analyzerDiagnostics, enabledSeverities);
            IReadOnlyList<CheckerDiagnostic> orderedDiagnostics =
                CliDiagnosticOrdering.Stable(filteredDiagnostics);

            string output = CliOutputRenderer.Render(
                options.OutputFormat,
                targetPath,
                configurationResult.ConfigurationPath,
                orderedDiagnostics);

            WriteOutput(output, options.OutputPath, standardOutput);

            return (int)CliExitCodeEvaluator.ForSuccessfulAnalysis(
                orderedDiagnostics,
                options.FailureThreshold);
        }
        catch (CliUsageException exception)
        {
            standardError.WriteLine($"入力エラー: {exception.Message}");
            standardError.WriteLine(UsageLine);
            return (int)CliExitCode.InputError;
        }
        catch (ConfigurationException exception)
        {
            standardError.WriteLine($"設定エラー: {exception.Message}");
            return (int)CliExitCode.InputError;
        }
        catch (UnsupportedInputException exception)
        {
            standardError.WriteLine($"未対応の解析対象: {exception.Message}");
            return (int)CliExitCode.InputError;
        }
        catch (FileNotFoundException exception)
        {
            standardError.WriteLine($"入力エラー: {exception.Message}");
            return (int)CliExitCode.InputError;
        }
        catch (UnauthorizedAccessException exception)
        {
            standardError.WriteLine($"入力エラー: {exception.Message}");
            return (int)CliExitCode.InputError;
        }
        catch (IOException exception)
        {
            standardError.WriteLine($"入出力エラー: {exception.Message}");
            return (int)CliExitCode.InputError;
        }
        catch (Exception exception)
        {
            standardError.WriteLine($"チェッカー内部エラー: {exception.Message}");
            return (int)CliExitCode.AnalysisFailure;
        }
    }

    private static void WriteOutput(
        string content,
        string? outputPath,
        TextWriter standardOutput)
    {
        if (outputPath is null)
        {
            standardOutput.WriteLine(content);
            return;
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullOutputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            fullOutputPath,
            content + Environment.NewLine);
    }

    private static string ProductVersion =>
        typeof(CliApplication).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? typeof(CliApplication).Assembly.GetName().Version?.ToString(3)
        ?? "unknown";

    private const string UsageLine =
        "使用方法: all-code-checker <target> [options]";

    private const string HelpText =
        """
        使用方法: all-code-checker <target> [options]

        options:
          --config <path>                         設定ファイルを明示指定
          --no-danger                             危険diagnosticを表示・判定対象から除外
          --no-warning                            警告diagnosticを表示・判定対象から除外
          --no-notice                             注意diagnosticを表示・判定対象から除外
          --coverage                              coverage実行（後続実装で有効化）
          --fail-on <danger|warning|notice|never> CI失敗しきい値（既定: danger）
          --format <text|json|sarif|github>        出力形式（既定: text）
          --output <path>                         出力先ファイル
          --version, -V                           version表示
          --help, -h                              help表示
        """;
}
