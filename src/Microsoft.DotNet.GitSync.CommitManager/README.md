# Microsoft.DotNet.GitSync.CommitManager

It runs on maestro triggers. It adds commits that need to be ported to the azure storage table.

## Arguments
The Arguments that need to be supplied to this tool are 

- -k = Azure Account Key
- -u = Azure Account Name
- -r = Repository to which commit is made
- -b = Branch to which commit is made
- -c = Sha of Commit(s)

[Related Documentation](../../Documentation/GitSyncTools.md )