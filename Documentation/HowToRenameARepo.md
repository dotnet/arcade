# Renaming or Moving a Repository

When a repo is renamed or moved, a number of things need to be done to ensure a seamless transition. These updates do not need to be done in any particular order

- Rename internal AzDO repository. Form should be: `<github org>-<repo name>'
- Rename instance of the github repo name in the classic Maestro's subscriptions file https://github.com/dotnet/versions/blob/master/Maestro/subscriptions.json 
- Update Maestro default channels to point to the new repo name
- Update Maestro subscriptions to pull from/to the new repo name.
- Update the `repositories` variable in GatherDropOperation to point to the new repo name, if that repo is present in the list: https://github.com/dotnet/arcade-services/blob/d2ef862c8262c3945a0c2098c3307ae9e264124a/src/Microsoft.DotNet.Darc/src/Darc/Operations/GatherDropOperation.cs#L336-L380.