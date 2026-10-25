param(
    [switch]$Check,
    [string[]]$Paths = @('src/AllCodeChecker.TypeScript/verify-published.ps1', 'tools/format-powershell.ps1')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$cache = Join-Path $root '.formatter-cache'
$version = '1.25.0'
$manifest = Join-Path $cache "PSScriptAnalyzer/$version/PSScriptAnalyzer.psd1"
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$rootPrefix = [System.IO.Path]::GetFullPath($root).TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
[System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    Save-PSResource -Name PSScriptAnalyzer -Version $version -Repository PSGallery -Path $cache -TrustRepository
}
Import-Module $manifest -ErrorAction Stop

$changed = @()
foreach ($relativePath in $Paths) {
    $path = [System.IO.Path]::GetFullPath((Join-Path $root $relativePath))
    if (-not $path.StartsWith($rootPrefix, $comparison)) {
        throw "Formatting target must stay inside the repository: $relativePath"
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Formatting target does not exist: $relativePath"
    }
    $source = [System.IO.File]::ReadAllText($path)
    $formatted = Invoke-Formatter -ScriptDefinition $source
    if ($source -cne $formatted) {
        $changed += $relativePath
        if (-not $Check) {
            [System.IO.File]::WriteAllText($path, $formatted, [System.Text.UTF8Encoding]::new($false))
        }
    }
}

if ($Check -and $changed.Count -gt 0) {
    throw "PowerShell files need formatting: $($changed -join ', ')"
}
Write-Output "PowerShell formatting $($Check ? 'check' : 'run') passed for $($Paths -join ', ')."
