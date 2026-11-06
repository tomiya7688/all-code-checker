using System.Diagnostics;
using AllCodeChecker.Analysis;
using AllCodeChecker.Discovery;

namespace AllCodeChecker.Core.Tests;

internal sealed class AnalyzerFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "all-code-checker-analyzer", Guid.NewGuid().ToString("N"));

    public string Write(string relativePath, string content)
    {
        string path = Path.GetFullPath(Path.Combine(Root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string Project(string relativePath, string extraItems = "") => Write(relativePath,
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          {extraItems}
        </Project>
        """);

    public static async Task RestoreAsync(string path)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in new[] { "restore", path, "--ignore-failed-sources", "-p:NuGetAudit=false" })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Fixture restore failed: {await output}\n{await error}");
        }
    }

    public static Task<AnalysisOrchestrationResult> AnalyzeAsync(string input, params ILanguageAnalyzer[] analyzers) =>
        new AnalyzerOrchestrator(analyzers).AnalyzeAsync(new AnalysisInputDiscovery().Discover(input));

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
