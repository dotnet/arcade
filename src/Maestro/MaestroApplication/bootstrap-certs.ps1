<#
    .Synopsis
      Bootstraps the certificate required to run the service fabric application locally
#>
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
  Write-Warning "Script must be run in Admin Mode!"
  exit 1
}

$ErrorActionPreference = "Stop";
Set-StrictMode -Version 2.0

function Get-KeyVaultCertBytes(
  [string]$VaultName,
  [string]$Name
) {
  Write-Host "Getting certificate '$name' from Key Vault '$VaultName'"
  $secret = Get-AzureKeyVaultSecret -VaultName $VaultName -Name $Name
  $pfxData = $secret.SecretValueText
  $pfxBytes = [Convert]::FromBase64String($pfxData)
  return $pfxBytes
}

function Import-CertBytes(
  $certBytes,
  $storeName
) {
  $store = New-Object System.Security.Cryptography.X509Certificates.X509Store -ArgumentList $storeName,LocalMachine
  try {
    $store.Open("ReadWrite");
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @([byte[]]$certBytes,"",[System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]"Exportable, PersistKeySet, MachineKeySet")
    $store.Add($cert)
    return $cert
  }
  catch {
    throw $_
  }
  finally {
    $store.Close();
  }
}

function Add-NetworkServiceReadAccess(
  [System.Security.Cryptography.X509Certificates.X509Certificate2]$Cert
) {
  $keyContainer = $cert.PrivateKey.CspKeyContainerInfo.UniqueKeyContainerName
  $certKeyPath = "C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys\$keyContainer";
  Write-Host "Setting ACL on $certKeyPath"
  $acl = Get-Acl $certKeyPath
  $rule = New-Object System.Security.AccessControl.FileSystemAccessRule @("NETWORK SERVICE", "Read", "Allow")
  $acl.AddAccessRule($rule)
  Set-Acl $certKeyPath $acl
}

Login-AzureRmAccount

$keyVaultCert = Get-KeyVaultCertBytes -Name "localhost" -VaultName maestrolocal
$cert = Import-CertBytes -certBytes $keyVaultCert -storeName "My"
Add-NetworkServiceReadAccess -Cert $cert
$unused = Import-CertBytes -certBytes $keyVaultCert -storeName "Root"
