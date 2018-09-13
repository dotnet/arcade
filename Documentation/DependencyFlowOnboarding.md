# Dependency flow onboarding

There's a set of steps that need to be completed so the versions of assets your repository depends on are updated and also the assets your repository produces are updated in upstream repositories.

## Copy the eng/ folder

Copy the `eng/` folder from the [minimalci-sample](https://github.com/dotnet/arcade-minimalci-sample) repo. 

This folder contains required version files as well as build definition templates used to publishing assets.

For more information about version files go to: https://github.com/dotnet/arcade/blob/master/Documentation/DependencyDescriptionFormat.md

## Enabling assets publishing to the Build Asset Registry (BAR)

To enable asset publishing to BAR we need to add a closing phase to `.vsts-ci.yml`. To do this add the following snippet at the end of `.vsts-ci.yml` and update the `dependsOn` parameter with the names 
of **all** the previous phases:

```
  - ${{ if and(ne(variables['System.TeamProject'], 'public'), notin(variables['Build.Reason'], 'PullRequest')) }}:
    - template: /eng/common/templates/phases/publish-build-assets.yml
      parameters:
        dependsOn:
          - phase1
          - phase2
        queue: 
          name: DotNetCore-Windows
```

## Add subscriptions and channels

### Join the `arcade-contrib` group

1. Go to https://github.com/orgs/dotnet/teams/arcade-contrib/members
2. Click on "Request to join"

### Get a token

Once you are part of the `arcade-contrib` group

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/
2. Click "Sign in" in the upper right of the page
3. Give consent to "DotNet Maestro"
4. Click on your name and then on "Tokens"
5. Choose a name for your token and then "Create"
6. Copy the created token

### Create a subscription

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html anc click on "Authorize"
2. In the "Value" input box add "Bearer" + the token generated in the previous step. i.e "Bearer m1T0ken6tab5" and click "Authorize"
3. Expand "POST /api/subscriptions" under "Subscriptions" and click "Try it out"
4. Update the values of the sample body. Here is an example of how would a request body look like:
``` json
{
    "channelName": ".NET Tools - Latest",
    "sourceRepository": "https://github.com/dotnet/arcade",
    "targetRepository": "https://github.com/dotnet/arcade-minimalci-sample",
    "targetBranch": "master",
    "policy": {
      "updateFrequency": "everyBuild",
      "mergePolicies": [
        {
          "name": "AllChecksSuccessful",
          "properties": {
            "ignoreChecks": [
              "WIP",
              "license/cla"
            ]
          }
        }
      ]
    }
}
```
5. Click "Execute"

### Create a channel 

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html anc click on "Authorize"
2. In the "Value" input box add "Bearer" + the token generated in the previous step. i.e "Bearer m1T0ken6tab5" and click "Authorize"
3. Expand "POST /api/channels" under "Channels" and click "Try it out"
4. Provide a "name" and a "classification"
5. Click "Execute"

Currently Swagger is the only way to create Subscriptions and Channels but the plan is for `Darc` to support this as well.


