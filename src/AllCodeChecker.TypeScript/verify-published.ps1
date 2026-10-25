$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runnerTemp = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [System.IO.Path]::GetTempPath() }
$work = Join-Path $runnerTemp ('all-code-checker-ts-smoke-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $work 'publish'
$fixture = Join-Path $work 'fixture'
$source = Join-Path $fixture 'index.ts'

$architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
$architectureName = switch ($architecture) {
    X64 { 'x64' }
    Arm64 { 'arm64' }
    default { throw "Unsupported architecture: $architecture" }
}
$platformName = if ($IsWindows) { 'win' } elseif ($IsMacOS) { 'osx' } elseif ($IsLinux) { 'linux' } else {
    throw 'Unsupported operating system.'
}
$rid = "$platformName-$architectureName"

New-Item -ItemType Directory -Path $fixture -Force | Out-Null
'{"compilerOptions":{"strict":true,"noEmit":true},"include":["*.ts"]}' |
    Set-Content -LiteralPath (Join-Path $fixture 'tsconfig.json') -Encoding utf8NoBOM
'const invalid: number = "text";' | Set-Content -LiteralPath $source -Encoding utf8NoBOM

Push-Location $projectRoot
try {
    $publishArgs = @('publish', 'src/AllCodeChecker.Cli/AllCodeChecker.Cli.csproj',
        '-c', 'Release', '-r', $rid, '--self-contained', 'true', '-o', $publish)
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid." }
}
finally {
    Pop-Location
}

$runtimeDirectory = Join-Path $publish 'typescript-runtime'
$node = Join-Path $runtimeDirectory $(if ($IsWindows) { 'node.exe' } else { 'node' })
$compiler = Join-Path $runtimeDirectory 'node_modules/typescript/package.json'
$helper = Join-Path $runtimeDirectory 'analyze.cjs'
$app = Join-Path $publish $(if ($IsWindows) { 'all-code-checker.exe' } else { 'all-code-checker' })
foreach ($required in @($node, $compiler, $helper, $app)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Published package is missing: $required"
    }
}

$savedPath = $env:PATH
try {
    $env:PATH = ''
    $output = & $app $fixture --format json --fail-on danger
    $exitCode = $LASTEXITCODE
}
finally {
    $env:PATH = $savedPath
}

if ($exitCode -ne 1) { throw "Expected diagnostic exit code 1; actual: $exitCode. Output: $output" }
$result = ($output -join [Environment]::NewLine) | ConvertFrom-Json
$matching = @($result.diagnostics | Where-Object {
        $_.rule -eq 'ACI001' -and $_.language -eq 'typescript' -and
        $_.reason -match 'TS2322' -and $_.path -eq $source
    })
if ($matching.Count -ne 1) { throw "Expected one TypeScript semantic diagnostic. Output: $output" }
if (@($result.diagnostics | Where-Object { $_.rule -eq 'ACI902' -or $_.rule -eq 'ACI903' }).Count -gt 0) {
    throw "Published helper failed. Output: $output"
}

$pythonSource = Join-Path $fixture 'valid.py'
'value = 1' | Set-Content -LiteralPath $pythonSource -Encoding utf8NoBOM
$savedPath = $env:PATH
try {
    $env:PATH = ''
    $pythonOutput = & $app $pythonSource --format json --fail-on danger
    $pythonExitCode = $LASTEXITCODE
}
finally {
    $env:PATH = $savedPath
}
if ($pythonExitCode -ne 0) { throw "Published Python analyzer failed with exit $pythonExitCode. Output: $pythonOutput" }
$pythonResult = ($pythonOutput -join [Environment]::NewLine) | ConvertFrom-Json
if (@($pythonResult.diagnostics | Where-Object { $_.rule -eq 'ACI902' -or $_.rule -eq 'ACI903' }).Count -gt 0) {
    throw "Published Python analyzer failed. Output: $pythonOutput"
}

Write-Output "Published TypeScript and Python CLI smoke passed on $rid without host PATH."
exit 0
