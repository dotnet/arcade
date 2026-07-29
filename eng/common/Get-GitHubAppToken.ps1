# Mints a short-lived GitHub App installation access token by signing a JWT
# with a private key stored in Azure Key Vault (RSA, RS256). The signed JWT is
# exchanged with the GitHub API for a token scoped to a single installation.
#
# Requirements:
#   - A GitHub App whose private key has been uploaded into Key Vault as an RSA
#     key (the PEM converted to a Key Vault *key*, NOT stored as a secret).
#   - The caller (the federated Azure service connection used to run this script)
#     must have the `Key Vault Crypto User` role (or at minimum the `Sign`
#     action) on that key.
#   - The App must be installed on the target organization/account
#     (`InstallationOwner`) with the permissions/repositories it needs.
#
# Installation tokens (ghs_*) are exempt from the enterprise classic-PAT
# lifetime policy, which is why this replaces the long-lived PAT.

[CmdletBinding()]
param(
    # Name of the Key Vault that holds the GitHub App's RSA signing key.
    [Parameter(Mandatory = $true)]
    [string] $KeyVaultName,

    # Name of the RSA key inside the Key Vault (the App's private key).
    [Parameter(Mandatory = $true)]
    [string] $KeyName,

    # The GitHub App's Client ID (the value to put in the `iss` JWT claim).
    [Parameter(Mandatory = $true)]
    [string] $AppClientId,

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
$PSNativeCommandUseErrorActionPreference = $true

function ConvertTo-Base64Url([byte[]] $bytes) {
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
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
    iss = $AppClientId
}

$headerEncoded  = ConvertTo-Base64Url ([System.Text.Encoding]::UTF8.GetBytes(($jwtHeader  | ConvertTo-Json -Compress)))
$payloadEncoded = ConvertTo-Base64Url ([System.Text.Encoding]::UTF8.GetBytes(($jwtPayload | ConvertTo-Json -Compress)))
$signingInput   = "$headerEncoded.$payloadEncoded"

# Key Vault `sign` expects the *digest* (base64), not the raw bytes.
$sha256       = [System.Security.Cryptography.SHA256]::Create()
$digestBytes  = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($signingInput))
$digestBase64 = [Convert]::ToBase64String($digestBytes)

Write-Host "Signing JWT with key '$KeyName' in vault '$KeyVaultName'..."
$signResponseJson = az keyvault key sign `
    --vault-name $KeyVaultName `
    --name $KeyName `
    --algorithm RS256 `
    --digest $digestBase64
$signResponse  = $signResponseJson | ConvertFrom-Json
$signatureUrl  = $signResponse.signature.TrimEnd('=').Replace('+', '-').Replace('/', '_')
$jwt           = "$signingInput.$signatureUrl"

$headers = @{
    Authorization          = "Bearer $jwt"
    'X-GitHub-Api-Version' = '2022-11-28'
    Accept                 = 'application/vnd.github+json'
    'User-Agent'           = 'dotnet-arcade-onelocbuild'
}

Write-Host "Looking up installation for '$InstallationOwner'..."
$installations = Invoke-RestMethod -Uri 'https://api.github.com/app/installations' -Headers $headers -Method Get
$installation  = $installations | Where-Object { $_.account.login -eq $InstallationOwner }
if ($null -eq $installation) {
    $found = ($installations | ForEach-Object { $_.account.login }) -join ', '
    Write-Error "No installation found for '$InstallationOwner'. App is installed on: $found"
    exit 1
}

$tokenResponse = Invoke-RestMethod `
    -Uri "https://api.github.com/app/installations/$($installation.id)/access_tokens" `
    -Headers $headers `
    -Method Post `
    -ContentType 'application/json'

Write-Host "Got installation token for '$InstallationOwner' (expires $($tokenResponse.expires_at))."
if ($OutputVariableName) {
    Write-Host "Setting pipeline variable '$OutputVariableName'."
    Write-Host "##vso[task.setvariable variable=$OutputVariableName;issecret=true]$($tokenResponse.token)"
}
else {
    Write-Host $tokenResponse.token -ForegroundColor Green
}
