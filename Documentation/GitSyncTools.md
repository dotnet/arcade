# Overview

This Gitsync tools helps to port commits from one Github repository to other. The commits are ported to keep the shared folder in sync with each other for repos [corefx](http://github.com/dotnet/corefx/), [coreclr](http://github.com/dotnet/coreclr/) and [corert](http://github.com/dotnet/corert/). It also preserves history.

## [Microsoft.DotNet.GitSync.CommitManager](../src/Microsoft.DotNet.GitSync.CommitManager/README.md)

It runs on maestro triggers. It adds commits that need to be ported to the azure storage table.

## [Microsoft.DotNet.GitSync](../src/Microsoft.DotNet.GitSync/README.md)

It runs as a background service. It does the following jobs.
- It reads records from the azure table and does a series of further checks to confirm if the commit needs to be mirrored.
- It then opens up the PR in the respective repositories, adds the assignees and waits for new candidates.