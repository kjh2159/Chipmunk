param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$installerScript = Join-Path $projectRoot "installer\Chipmunk.iss"

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "publish.ps1") -Mode Portable
}

$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$isccPath = if ($null -ne $isccCommand) { $isccCommand.Source } else { $null }

if ([string]::IsNullOrWhiteSpace($isccPath)) {
    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates += Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates += Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"
    }
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $candidates += Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
    }

    $isccPath = $candidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($isccPath)) {
    throw "Inno Setup 6.4 or newer was not found. Install it from https://jrsoftware.org/isdl.php and run this script again."
}

& $isccPath $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Installer: $(Join-Path $projectRoot 'artifacts\installer\Chipmunk-Setup-x64.exe')"
