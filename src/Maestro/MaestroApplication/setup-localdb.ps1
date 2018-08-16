if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
  Write-Warning "Script must be run in Admin Mode!"
  exit 1
}

sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
sqllocaldb share MSSQLLocalDB SharedLocal
sqllocaldb stop MSSQLLocalDB
sqllocaldb start MSSQLLocalDB

& "C:\Program Files\Microsoft SQL Server\110\Tools\Binn\sqlcmd.exe" -S "(localdb)\.\SharedLocal" -d "master" -Q "CREATE LOGIN [NT AUTHORITY\NETWORK SERVICE] FROM WINDOWS; ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\NETWORK SERVICE];"
