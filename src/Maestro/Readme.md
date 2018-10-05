# Maestro and the BAR

## Getting started
1. Install Azure Service Fabric SDK: https://www.microsoft.com/web/handlers/webpi.ashx?command=getinstallerredirect&appid=MicrosoftAzure-ServiceFabric-CoreSDK
2. Install SQL Server Express: https://www.microsoft.com/en-us/sql-server/sql-server-editions-express 
3. Acquire the required secrets from azure key vault. This can be done by running [bootstrap.ps1](bootstrap.ps1) from an admin powershell window. This script will do 3 things:
    - Download a secret required for using the `Microsoft.Azure.Services.AppAuthentication` package from the service fabric local dev cluster
    - Download and install the SSL cert used for local development from key vault
    - Configure the SQL Server LocalDB instance for use from the local service fabric cluster
4. From the Maestro.Data project directory, run `dotnet ef --msbuildprojectextensionspath <path to obj dir for Maestro data (e.g. artifacts/obj/Maestro.Data/)> database update`. 
5. Join the @maestro-auth-test org in GitHub.
6. In SQL Server Object Explorer in Visual Studio, find the local SQLExpression database for the build asset registry and populate the RepoInstallations table with the following row:
    - Repository: https://github.com/maestro-auth-test/maestro-test
    - Installation Id: 289474

After successfully running `bootstrap.ps1` running the `MaestroApplication` project via F5 in VS (launch as elevated) will run the application on `https://localhost:4430`

## Troubleshooting.

Things to try:
- Clean your repo before building/running.
- Ensure the ASP.NET Workload is installed for Visual Studio.
- Search the web for the error you are seeing.