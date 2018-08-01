# Microsoft.DotNet.GitSync.CommitManager

It runs on maestro triggers. It adds commits that need to be ported to the azure storage table.
Webhook sends the payload to the maestro after every push event in any of the 3 repositories (corefx, coreclr, corert).
The payload contains the path of the changed files in that commit. If any of these path is in the shared folder, then that commit is a candidate for azure table.

## Arguments
The Arguments that need to be supplied to this tool are 

- -k = Azure Account Key
- -u = Azure Account Name
- -r = Repository to which commit is made
- -b = Branch to which commit is made
- -c = Sha of Commit(s)

```
dotnet run Microsoft.DotNet.GitSync.CommitManager.csproj -- -u $(AccountName) -k *** -r dotnet/coreclr -c 5d31194880e800a9df8eef76e7a0a53646aa72d3 -b master
```

[Related Documentation](../../Documentation/GitSyncTools.md )