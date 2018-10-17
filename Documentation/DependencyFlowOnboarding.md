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

### 3.2. Set up your darc client.

Once you are part of the `arcade-contrib` team

1. Go to https://maestro-prod.westus2.cloudapp.azure.com/
2. Click "Sign in" in the upper right of the page
3. Give consent to "DotNet Maestro"
4. Click on your name and then on "Tokens"
5. Choose a name for your token and then "Create"
6. Copy the created token
7. Open a powershell or bash prompt and navigate to a repository that has the arcade toolset.
8. Run `.\eng\common\darc-init.ps1` or `.\eng\common\darc-init.sh`.  This will install darc as a global tool.
9. Run `darc authenticate`
10. Place the token into the `bar_password` field.  You may leave the rest of the fields as-is.
11. Save and close.

### 3.3. Get all existing channels

1. Run `darc get-channels` to display available channels.  Arcade builds are published to the '.NET Tools - Latest' channel.

### 3.4. Create a subscription to get Arcade updates

Darc can be used to create new subscriptions, either in interactive mode or non-interactive mode.
Interactive will open an editor to modify the fields, while non-interactive expects all fields on the command line.

**Interactive mode**

1. Run `darc add-subscription`
2. Fill out the fields.  For Arcade, this typically looks like:
```
Channel: .NET Tools - Latest
Source Repository URL: https://github.com/dotnet/arcade
Target Repository URL: <your repository URL>
Target Branch: <target branch for arcade updates, e.g. master>
Update Frequency: everyDay
Merge Policies:
- Name: AllChecksSuccessful
  Properties:
    ignoreChecks:
    - WIP
    - license/cla
```
3. Save and close.

**Non-interactive mode**

1. Run `darc add-subscription --channel ".NET Tools Latest" --source-repo https://github.com/dotnet/arcade --target-repo <your repo> --target-branch master --update-frequency everyDay --ignore-checks WIP,license/cla --all-checks-passed -q`

These steps can be altered for additional subscriptions to other repositories.

### 3.5. Create a channel (optional, typically not needed)

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
