# Maestro and the BAR

## Running locally
Before running Maestro and the Build Asset Registry locally you must first acquire the required secrets from azure key vault. This can be done by running [bootstrap.ps1](bootstrap.ps1) from an admin powershell window. This script will do 3 things:
* Download a secret required for using the `Microsoft.Azure.Services.AppAuthentication` package from the service fabric local dev cluster
* Download and install the SSL cert used for local development from key vault
* Configure the SQL Server LocalDB instance for use from the local service fabric cluster

After successfully running `bootstrap.ps1` running the `MaestroApplication` project via F5 in VS will run the application on `https://localhost:4430`

