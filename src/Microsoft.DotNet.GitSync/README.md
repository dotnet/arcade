# Microsoft.DotNet.GitSync

This tool reads records from the table and does a series of further checks to determine if the commit needs to be mirrored.
The first check is whether the commit is a merged commit. If yes, then we skip this commit as we have already ported the individual commits from that push event.
The second check is whether the commit is a mirrored commit (Commit that is made through GitSync Tool). If yes, we skip this too.
It then opens up the PR in the respective repositories, adds the assignees and waits for new candidates.

## Configuration File
The configuration file has many sections. Some of them are described below

```Json
{
  "Repos": [ ],
  "UserName": "",
  "Branches": [ ],
}
```

The properties have the following semantics:
- Repos: It is the list of the repos that need to be synced. It includes basic information about the repositories like shared path, owner, name etc
- UserName: Username of the GitHub account which will be used to open PullRequests in the dotnet repositories to mirror the commits. 
- Branches: It is the list of the branches that need to be synced for all the repos.

[Related Documentation](../../Documentation/GitSyncTools.md )