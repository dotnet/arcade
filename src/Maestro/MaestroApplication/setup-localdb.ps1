if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
  Write-Warning "Script must be run in Admin Mode!"
  exit 1
}

& "C:\Program Files\Microsoft SQL Server\110\Tools\Binn\sqlcmd.exe" -S "localhost\SQLEXPRESS" -d "master" -Q "CREATE LOGIN [NT AUTHORITY\NETWORK SERVICE] FROM WINDOWS; ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\NETWORK SERVICE];"
