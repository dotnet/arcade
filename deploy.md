# Maestro deployment

The Maestro web application is deployed at <http://maestro-prod.azurewebsites.net>.  It is currently published in the [maestro-prod app service](https://ms.portal.azure.com/#resource/subscriptions/68672ab8-de0c-40f1-8d1b-ffb20bd62c0f/resourcegroups/maestro/providers/Microsoft.Web/sites/maestro-prod/appServices) in the HelixProd Azure subscription.

## Logs
The logs are in the [maestroprodstorage Storage Account](https://ms.portal.azure.com/#resource/subscriptions/68672ab8-de0c-40f1-8d1b-ffb20bd62c0f/resourceGroups/maestro/providers/Microsoft.Storage/storageAccounts/maestroprodstorage/overview).  The CSV files can be viewed by opening the maestro-logs blob container in Azure or your favorite blob storage viewer (e.g. Cloud Explorer in VS or Storage Explorer).

## Source code change: Publish
Whenever a change to the Maestro source code is submitted, the app needs to be published.  This is currently a manual process.

 1. Ensure that the local dotnet/versions tree is in sync with the master branch on GitHub.
 2. Open Maestro\Microsoft.DotNet.Maestro.sln in VS.
 3. Remove the "AzureWebJobsStorage" connection string from Web.config.  (This connection string is not needed in Web.config when publishing because it is defined in App Settings in Azure, but it's defined with an empty value in Web.config in source control so that it can be easily set when F5'ing.)
 4. Build the solution.
 5. Right-click on the Microsoft.DotNet.Maestro.WebApi project, and select Publish.
 6. Select “Microsoft Azure App Service”.
 7. Select the “maestro-prod” app service and click OK.
 8. On the Settings page of the publish wizard, select the Release configuration.
 9. Click Publish.
 10. Right-click on the Microsoft.DotNet.Maestro.WebJob project, and select Publish as Azure WebJob.
 11. Repeat steps 5-8.
 12. Locally undo the removal of the connection string in Web.config, and the adding of the publish profiles to the csprojs.

## Old deployment locations
 * In Eric Erhardt's Azure subscription (may have been removed)
   * App service: [dotnet-maestro app service](https://ms.portal.azure.com/?flight=1#resource/subscriptions/3220c2af-392a-4438-bde4-16fc1d50b593/resourceGroups/dotnetclidepsmirror/providers/Microsoft.Web/sites/dotnet-maestro/appServices)
   * Log blob storage: [maestroapp Storage Account](https://ms.portal.azure.com/?flight=1#resource/subscriptions/3220c2af-392a-4438-bde4-16fc1d50b593/resourceGroups/dotnetclidepsmirror/providers/Microsoft.Storage/storageAccounts/maestroapp/overview)
