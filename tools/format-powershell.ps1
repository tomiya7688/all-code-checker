#Requires -Version 7.0
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
    $separators = @([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $segments = $path.Substring($rootPrefix.Length).Split($separators, [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($segments[0] -in @('.git', '.formatter-cache') -or
        [System.IO.Path]::GetExtension($path) -notin @('.ps1', '.psm1', '.psd1')) {
        throw "Formatting target must be a source PowerShell file: $relativePath"
    }
    $current = $rootPrefix.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    foreach ($segment in $segments) {
        $current = Join-Path $current $segment
        if ((Test-Path -LiteralPath $current) -and
            ([System.IO.File]::GetAttributes($current) -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw "Formatting target cannot contain a symbolic link or junction: $relativePath"
        }
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Formatting target does not exist: $relativePath"
    }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and
    $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasUtf8Bom) { 3 } else { 0 }
    $source = [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes, $offset, $bytes.Length - $offset)
    $formatted = Invoke-Formatter -ScriptDefinition $source
    if ($source -cne $formatted) {
        $changed += $relativePath
        if (-not $Check) {
            $temporaryPath = Join-Path (Split-Path -Parent $path) ('.format-' + [guid]::NewGuid().ToString('N') + '.tmp')
            try {
                [System.IO.File]::WriteAllText($temporaryPath, $formatted, [System.Text.UTF8Encoding]::new($hasUtf8Bom))
                [System.IO.File]::Move($temporaryPath, $path, $true)
            }
            finally {
                if (Test-Path -LiteralPath $temporaryPath) {
                    Remove-Item -LiteralPath $temporaryPath -Force
                }
            }
        }
    }
}

if ($Check -and $changed.Count -gt 0) {
    throw "PowerShell files need formatting: $($changed -join ', ')"
}
if (-not $Check -and $changed.Count -gt 0) {
    Write-Output "Formatted: $($changed -join ', ')"
}
Write-Output "PowerShell formatting $($Check ? 'check' : 'run') passed for $($Paths -join ', ')."
