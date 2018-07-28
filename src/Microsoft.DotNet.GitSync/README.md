# Microsoft.DotNet.GitSync

This tool ports commits from one GitHub repository to other. The commits are ported to keep the shared folder in sync with each other for repos [corefx](http://github.com/dotnet/corefx/), [coreclr](http://github.com/dotnet/coreclr/) and [corert](http://github.com/dotnet/corert/). It also preserves history.

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