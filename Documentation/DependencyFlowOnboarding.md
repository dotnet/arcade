# Dependency flow onboarding

There's a set of steps that need to be completed so the versions of assets your repository depends on are updated and also the assets your repository produces are updated in upstream repositories.

## 1. Copy the eng/ folder

Copy the `eng/` folder from the [minimalci-sample](https://github.com/dotnet/arcade-minimalci-sample) repo. 

This folder contains required version files as well as build definition templates used for publishing assets.

For more information about version files go to: https://github.com/dotnet/arcade/blob/master/Documentation/DependencyDescriptionFormat.md

## 2. Enable assets publishing to the Build Asset Registry (BAR)

To enable asset publishing to BAR we need to add a closing phase to `.vsts-ci.yml`. To do this add the following snippet at the end of `.vsts-ci.yml` and update the `dependsOn` parameter with the names of **all** the previous **build** phases:

```
  - ${{ if and(ne(variables['System.TeamProject'], 'public'), notin(variables['Build.Reason'], 'PullRequest')) }}:
    - template: /eng/common/templates/phases/publish-build-assets.yml
      parameters:
        dependsOn:
          - phase1
          - phase2
        queue: 
          name: Hosted VS2017
```

## 3. Add subscriptions and channels

### 3.1. Join the `arcade-contrib` team

1. Go to https://github.com/orgs/dotnet/teams/arcade-contrib/members
2. Click on "Request to join"

### 3.2. Create a PAT for interacting with Maestro

Once you are part of the `arcade-contrib` team

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/
2. Click "Sign in" in the upper right of the page
3. Give consent to "DotNet Maestro"
4. Click on your name and then on "Tokens"
5. Choose a name for your token and then "Create"
6. Copy the created token

### 3.3. Get all existing channels

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html and click on "Authorize"
2. In the "Value" input box add "Bearer" + the token generated in the previous step. i.e "Bearer m1T0ken6tab5" and click "Authorize"
3. Expand "GET /api/channels" under "Channels" and click "Try it out"
5. Click "Execute"

### 3.4. Create a subscription

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html and click on "Authorize"
2. In the "Value" input box add "Bearer" + the token generated in the previous step. i.e "Bearer m1T0ken6tab5" and click "Authorize"
3. Expand "POST /api/subscriptions" under "Subscriptions" and click "Try it out"
4. Update the values of the sample body. Here is an example of how would a request body look like:
``` json
{
    "channelName": "an existing channel from step 3.3. current arcade builds output to '.NET Tools - Latest'",
    "sourceRepository": "the repository flowing the versions i.e. https://github.com/dotnet/arcade",
    "targetRepository": "the repository getting updated with the new versions i.e. https://github.com/dotnet/arcade-minimalci-sample",
    "targetBranch": "branch in the targetRepository i.e master",
    "policy": {
      "updateFrequency": "one of 'none', 'everyDay', 'everyBuild'",
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

### 3.5. Create a channel (optional)

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html and click on "Authorize"
2. In the "Value" input box add "Bearer" + the token generated in the previous step. i.e "Bearer m1T0ken6tab5" and click "Authorize"
3. Expand "POST /api/channels" under "Channels" and click "Try it out"
4. Provide a "name" and a "classification"
5. Click "Execute"

### 3.6. Associate a branch with a channel (optional)

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html and click on "Authorize"
2. In the "Value" input box add "Bearer" + the token generated in the previous step. i.e "Bearer m1T0ken6tab5" and click "Authorize"
3. Expand "POST /api/default-channels" under "DefaultChannels" and click "Try it out"
4. Update the values of the sample body. Here is an example of how would a request body look like:
``` json
{
  "repository": "onboarded repo i.e. https://github.com/dotnet/arcade",
  "branch": "onboarded brach i.e. refs/heads/master",
  "channelId": id-of-existing-channel
}
```
5. Click "Execute"

Currently the REST API is the only way to create Subscriptions and Channels but the plan is for `Darc` to support this as well.

## 4. Validate

At this time we don't have a way to notify users if something went wrong while updating dependencies but this work is tracked by
https://github.com/dotnet/arcade/issues/821.

To validate that created subscriptions and channels work as expected you'd need to verify that a PR has been created on your subscription's `targetRepository` once a build from `sourceRepository` has successfully completed. If a PR was not created something went wrong and to determine what went wrong we need to query the REST API by following these steps:

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/swagger/ui/index.html and click on "Authorize"
2. In the "Value" input box add "Bearer" + the token generated in the previous step. i.e "Bearer m1T0ken6tab5" and click "Authorize"
3. Expand "GET /api/subscriptions/{id}/history" under "Subscriptions" and click "Try it out"
4. Provide your subscription id
5. Click "Execute"
6. Find an entry with `"success": false`
7. Let @alexperovich and @jcagme know about the errors in the unsuccessful entry
