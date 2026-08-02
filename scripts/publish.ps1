param(
    [ValidateSet("Portable", "SingleFile", "All")]
    [string]$Mode = "All"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "src\Chipmunk\Chipmunk.csproj"
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -ne $dotnetCommand) {
    $dotnetPath = $dotnetCommand.Source
}
else {
    $dotnetPath = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    if (-not (Test-Path -LiteralPath $dotnetPath -PathType Leaf)) {
        throw ".NET 8 SDK was not found. Install it or add dotnet.exe to PATH."
    }
}

function Invoke-PublishProfile {
    param([string]$Profile)

    Write-Host "Publishing profile: $Profile"
    & $dotnetPath publish $projectFile `
        -c Release `
        -p:Platform=x64 `
        -p:PublishProfile=$Profile
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for profile '$Profile'."
    }
}

if ($Mode -in @("Portable", "All")) {
    Invoke-PublishProfile "PortableSelfContained"
}

if ($Mode -in @("SingleFile", "All")) {
    Invoke-PublishProfile "SingleFile"
}

Write-Host "Artifacts: $(Join-Path $projectRoot 'artifacts')"
