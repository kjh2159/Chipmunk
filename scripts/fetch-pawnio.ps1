param(
    [string]$Destination
)

$ErrorActionPreference = "Stop"
$version = "2.2.0"
$expectedSha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032"
$downloadUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/$version/PawnIO_setup.exe"
$projectRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $projectRoot "installer\dependencies\PawnIO_setup.exe"
}

$destinationDirectory = Split-Path -Parent $Destination
New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
Invoke-WebRequest -Uri $downloadUrl -OutFile $Destination

$actualSha256 = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "PawnIO hash mismatch. Expected $expectedSha256, received $actualSha256."
}

$signature = Get-AuthenticodeSignature -LiteralPath $Destination
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
    throw "PawnIO Authenticode signature is not valid: $($signature.Status)."
}

if ($signature.SignerCertificate.Subject -notmatch "CN=namazso\.eu") {
    throw "Unexpected PawnIO signer: $($signature.SignerCertificate.Subject)."
}

Write-Host "Verified PawnIO $version"
Write-Host "SHA-256: $actualSha256"
Write-Host "Signer: $($signature.SignerCertificate.Subject)"
