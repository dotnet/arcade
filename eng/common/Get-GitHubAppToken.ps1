# Mints a short-lived GitHub App installation access token by signing a JWT
# with an RSA private key (RS256). The signed JWT is exchanged with the GitHub
# API for a token scoped to a single installation.
#
# Requirements:
#   - A GitHub App ID and PEM private key supplied through the environment.
#   - The App must be installed on the target organization/account
#     (`InstallationOwner`) with the permissions/repositories it needs.
#
# Installation tokens (ghs_*) are exempt from the enterprise classic-PAT
# lifetime policy, which is why this replaces the long-lived PAT.

[CmdletBinding()]
param(
    # Login of the organization or user account whose installation we should
    # mint the token for (e.g. `dotnet`, `microsoft`).
    [Parameter(Mandatory = $true)]
    [string] $InstallationOwner,

    # Optional Azure DevOps pipeline variable name to set with the installation
    # token (marked as a secret). When not specified, the token is written to
    # stdout instead.
    [Parameter(Mandatory = $false)]
    [string] $OutputVariableName
)
$ErrorActionPreference = 'Stop'

. $PSScriptRoot\pipeline-logging-functions.ps1

function ConvertTo-Base64Url([byte[]] $bytes) {
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$appId = $env:GITHUB_APP_ID
$privateKey = $env:GITHUB_APP_PRIVATE_KEY
if ([string]::IsNullOrWhiteSpace($appId) -or [string]::IsNullOrWhiteSpace($privateKey)) {
    Write-PipelineTelemetryError -Category 'Build' -Message "GITHUB_APP_ID and GITHUB_APP_PRIVATE_KEY must both be set. Verify the pipeline's Key Vault-backed variable group includes both github-app-secret values."
    exit 1
}
if ($appId -match '^\$\([^)]+\)$' -or $privateKey -match '^\$\([^)]+\)$') {
    Write-PipelineTelemetryError -Category 'Build' -Message "The GitHub App ID or private key is an unresolved pipeline variable. Verify the pipeline includes and is authorized to use its Key Vault-backed variable group."
    exit 1
}

# Build JWT header and payload. Use [ordered] hashtables so JSON
# serialization is deterministic.
$jwtHeader = [ordered]@{
    alg = 'RS256'
    typ = 'JWT'
}
$now = [System.DateTimeOffset]::UtcNow
$jwtPayload = [ordered]@{
    iat = $now.AddMinutes(-1).ToUnixTimeSeconds()
    exp = $now.AddMinutes(5).ToUnixTimeSeconds()
    iss = $appId
}

$headerEncoded  = ConvertTo-Base64Url ([System.Text.Encoding]::UTF8.GetBytes(($jwtHeader  | ConvertTo-Json -Compress)))
$payloadEncoded = ConvertTo-Base64Url ([System.Text.Encoding]::UTF8.GetBytes(($jwtPayload | ConvertTo-Json -Compress)))
$signingInput   = "$headerEncoded.$payloadEncoded"

$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $digestBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($signingInput))
}
finally {
    $sha256.Dispose()
}

Write-Host 'Signing JWT with the Secret Manager private key...'
$rsa = [System.Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem($privateKey)
    $signatureBytes = $rsa.SignHash(
        $digestBytes,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $signatureUrl = ConvertTo-Base64Url $signatureBytes
}
catch {
    Write-PipelineTelemetryError -Category 'Build' -Message "Failed to sign the GitHub App JWT with the Secret Manager private key: $_"
    exit 1
}
finally {
    $rsa.Dispose()
}
$jwt = "$signingInput.$signatureUrl"

$headers = @{
    Authorization          = "Bearer $jwt"
    'X-GitHub-Api-Version' = '2022-11-28'
    Accept                 = 'application/vnd.github+json'
    'User-Agent'           = 'dotnet-arcade-onelocbuild'
}

Write-Host "Looking up installation for '$InstallationOwner'..."
try {
    $installations = [System.Collections.Generic.List[object]]::new()
    $page = 1
    do {
        $pageResponse = Invoke-RestMethod `
            -Uri "https://api.github.com/app/installations?per_page=100&page=$page" `
            -Headers $headers `
            -Method Get
        $pageInstallationCount = 0
        foreach ($installation in $pageResponse) {
            $installations.Add($installation)
            $pageInstallationCount++
        }
        $page++
    } while ($pageInstallationCount -eq 100)
}
catch {
    Write-PipelineTelemetryError -Category 'Build' -Message "Failed to list GitHub App installations: $_. The signed JWT may be invalid or the App ID may be incorrect."
    exit 1
}
$matchingInstallations = @($installations | Where-Object { $_.account.login -ieq $InstallationOwner })
if ($matchingInstallations.Count -eq 0) {
    $found = ($installations | ForEach-Object { $_.account.login }) -join ', '
    Write-PipelineTelemetryError -Category 'Build' -Message "No installation found for '$InstallationOwner'. App is installed on: $found"
    exit 1
}
if ($matchingInstallations.Count -ne 1) {
    $matchingIds = ($matchingInstallations | ForEach-Object { $_.id }) -join ', '
    Write-PipelineTelemetryError -Category 'Build' -Message "Found multiple installations for '$InstallationOwner': $matchingIds"
    exit 1
}
$installation = $matchingInstallations[0]
Write-Host "Using installation $($installation.id) for '$($installation.account.login)'."

try {
    $tokenResponse = Invoke-RestMethod `
        -Uri "https://api.github.com/app/installations/$($installation.id)/access_tokens" `
        -Headers $headers `
        -Method Post `
        -ContentType 'application/json'
}
catch {
    Write-PipelineTelemetryError -Category 'Build' -Message "Failed to mint an installation access token for '$InstallationOwner' (installation $($installation.id)): $_"
    exit 1
}

Write-Host "Got installation token for '$InstallationOwner' (expires $($tokenResponse.expires_at))."
if ($OutputVariableName) {
    Write-Host "Setting pipeline variable '$OutputVariableName'."
    Write-Host "##vso[task.setvariable variable=$OutputVariableName;issecret=true]$($tokenResponse.token)"
}
else {
    Write-Host $tokenResponse.token -ForegroundColor Green
}
