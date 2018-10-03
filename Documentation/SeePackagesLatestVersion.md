### How to See What's the Latest Version of an Arcade Package

1. Follow the steps outlined [here](https://github.com/dotnet/arcade/blob/master/Documentation/DependencyFlowOnboarding.md#32-create-a-pat-for-interacting-with-maestro) to create a Maestro++ authorization token for you.
2. Go to [swagger API](https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html) and click on "Authorize" button on the top-right corner.
3. In the "Value" input box add "Bearer" + the token generated in the previous step. i.e "Bearer m1T0ken6tab5" and click "Authorize".
4. On the Swagger API go to [Builds -> Latest](https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html#/Builds/Builds_GetLatest) and click on the "***Try it out***" button on the right hand side.
5. Fill in the following fields:
   1. On the "repository" field inform the URL of the repository (e.g., https://github.com/arcade)
   2. On the "loadCollections" field select "true"
   3. Click on the "Execute" button
6. The "Response Body" field will contain information about the latest assets published.
