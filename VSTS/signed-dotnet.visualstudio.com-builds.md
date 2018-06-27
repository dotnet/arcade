# Signed dotnet.visualstudio.com builds

Dotnet.visualstudio.com does not have support for signed builds.  Code should still be mirrored to dotnet.visualstudio.com/internal as outlined in the [VSTS Guidance](https://github.com/dotnet/arcade/blob/master/Documentation/VSTS/VSTSGuidance.md#projects), but build definitions for signing should be created in devdiv.visualstudio.com with an "External Git" source which references the dotnet.visualstudio.com/internal git repository

1. Select a source: External Git
2. Change the Connection to "New Service Endpoint"
    - User name: dotnet-bot@microsoft.com
    - Password / Token Key: Listed in "EngKeyVault" as "dn-bot-dotnet-build-rw-code-rw"
      - You can get Read access to "EngKeyVault" by joining the "DncEngKvRead" [security group](https://idweb/identitymanagement/aspx/groups/AllGroups.aspx)
