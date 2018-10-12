# Sign Tool

This is a MSBuild custom task that provides batch signing and simple verification for MicroBuild environments. The tool is intended to be run as a post-build step and is able to automatically infer the files that need to be signed given a list of container files (.nupkg, .vsix, etc) as input. The high level features of this tool are:

- **Performance:** It operates as a post-build step and uses the minimum number of requests possible. This can have a dramatic performance improvement over the typical implementation which signs as a post-compile step. For example, in Roslyn it took build + sign times down from 1-2 hours to 8 minutes.
- **Automated:** It is able to recursively open container files (like .nupkg, .vsix) and create a list of nested files that need to be signed. Therefore, you don't need to manually specify which files need to be signed.
- **Strict mode:**
  - The tool supports receiving an *explicit list of files* that need to be signed or ignored from the signing process.
  - The tool will report an error if a [signable file](https://microsoft.sharepoint.com/teams/codesigninfo/Wiki/Signable%20Files.aspx) is discovered for which no signing information was found. That means that all signable files will need to be either explicitly ignored or have signing information attributed in one of the many ways explained below.
- **Containers:** It can handle the nesting issues that come with container files: both nested PE and nested containers. The tool will correctly sign the inner most content first, repack the containing package with the signed content and then sign the outermost container. Arbitrary levels of nesting are supported.
- **Post signing checks:** It takes extra steps to ensure that **PE files** are properly signed after the signing process completes. This tool parses the signed binary file to make sure the binary was modified and marked as signed.
- **Dual certificates:** The tool can receive a list of certificate descriptors which contain a property that can flag a certificate as dual. Dual certificates can be used to sign already signed files.

## Arguments

| Name                  | Type     | Required | Description                                                  |
| --------------------- | -------- | -------- | ------------------------------------------------------------ |
| DryRun                | bool     | No       | When true the list of files to be signed will be created but won't be sent to the signing server. Default is false. |
| TestSign              | bool     | No       | When true the binaries will be test signed. The default is to real sign. |
| ItemsToSign           | Array    | **Yes**  | This is a list of *full path* to files that need to be signed. Container files will be expanded to look for nested files that need to be signed. |
| StrongNameSignInfo    | Array    | No       | Should store the default certificate name and strong name to be used for a given Public Key Token. See details below. |
| FileSignInfo          | Array    | No       | Used to override the default certificate information for specific files and target frameworks combinations. If not specified default information is used or error occurs. See details below. |
| FileExtensionSignInfo | Array    | No       | This is a mapping between extension (in the format ".ext") to default sign information for those kind of files. Overriding of the default sign info is done using the other parameters. |
| CertificatesSignInfo  | Array    | No       | List of certificate names that can be flagged using the `DualSigningAllowed` attribute as dual certificates. |
| MicroBuildCorePath    | Dir Path | **Yes**  | Path to MicroBuild.Core package directory.                   |
| MSBuildPath           | Exe path | !DryRun  | Path to the MSBuild.exe binary used to run the signing process on MicroBuild. |
| TempDir               | Dir path | **Yes**  | Used to store temporary files during the process of calling MicroBuild signing. |
| LogDir                | Dir path | !DryRun  | MSBuild binary log information from the signing rounds will be stored in this directory. |

**Note:** `MSBuildPath` and `LogDir` are only required if `DryRun == false`.


# Arguments Metadata

**StrongNameSignInfo**

This field **requires** the following metadata: `PublicKeyToken`, `CertificateName` and the `Include` field is assumed to hold the `Strong Name`. This information will be used as the default certificate and strong name information for all **PE files** that match the `PublicKeyToken`.

**FileExtensionSignInfo**

This field requires two metadata attributes: `CertificateName` and `Include` which should be a file extension in the format `.ext`. This field is used to configure a default certificate for all files that have an specific extension.

**CertificatesSignInfo**

This field requires the following metadata: `DualSigningAllowed` (boolean) and `Include` which is assumed to hold a valid certificate name. Use this list to explicitly configure the tool to allow the use of the specified certificate as a dual certificate - i.e., be able to use it to sign already signed files.

**FileSignInfo**

This field accepts the following metadata: `PublicKeyToken` (*optional*), `CertificateName`, `TargetFramework` (*optional*) and the `Include` field is assumed to hold a file name (*including extension; not a full path*). The `CertificateName` attribute accept the value "*None*" to flag a file that should not be signed.

All files that match the combination informed will use the Signing information informed.

**ItemsToSign**

This field only attribute should be  `Include`. This field holds a list of full path to files that need to be considered during the signing process. Path to containers and regular files are accepted. Containers will be opened and their content will be processed recursively.

# Signing Info Precedence



## Example Usage

The Arcade SDK will include all container files from the `$(ArtifactsPackagesDir)` and `$(VisualStudioSetupOutputPath)` folders (these properties are set [here](../src/Microsoft.DotNet.Arcade.Sdk/tools/RepoLayout.props)) in the list of containers to be looked up for. Note that only projects marked with `<IsPackable>true</IsPackable>` will be packed and copied to this folder. Here is how the Arcade SDK does it:

```xml
<ItemGroup>
	<ItemsToSign Include="$(ArtifactsPackagesDir)**\*.nupkg" />
	<ItemsToSign Include="$(VisualStudioSetupOutputPath)**\*.vsix" />
</ItemGroup>
```

If you **need to** add additional file(s) make sure to add them to the `ItemsToSign` list. Default signing information (Certificate Name & Strong Name) will be looked up in the `StrongNameSignInfo` property based on the Public Key Token of the file. Therefore, make sure that `StrongNameSignInfo` contains a public key token covering the files that you add to `ItemsToSign`. Here's an example:

```xml
<ItemGroup>
	<StrongNameSignInfo Include="StrongName1" PublicKeyToken="31bf3856ad364e35" CertificateName="CertificateName1" />
</ItemGroup>
```

It is possible to override the default signing information or skip signing specific file(s). For that you need to use the `FileSignInfo` property. For instance, in the snippet below the certificate `MyCustomCert` will be used for `My.Library.dll` when it targets `.NETStandard,Version=v2.0` and has Public Key Token `31bf3856ad364e35`:

```xml
<ItemGroup> 
    <FileSignInfo Include="My.Library.dll" TargetFramework=".NETStandard,Version=v2.0" PublicKeyToken="31bf3856ad364e35" CertificateName="MyCustomCert" />
</ItemGroup>
```
In this snippet the library `Other.Library.dll` with Public Key Token `31bf3856ad364e35` won't be signed, independent of its Target framework:

```xml
	<FileSignInfo Include="Other.Library.dll" TargetFramework="All" PublicKeyToken="31bf3856ad364e35" CertificateName="None" />
</ItemGroup>
```
For more detailed information you can see [how](../src/Microsoft.DotNet.Arcade.Sdk/tools/Sign.proj) the Arcade SDK calls the `SignToolTask`. Here's a snippet:

```xml
...
<Microsoft.DotNet.SignTool.SignToolTask
    DryRun="$(DryRun)"
    TestSign="$(TestSign)"
    ItemsToSign="@(ItemsToSign)"
    StrongNameSignInfo="@(StrongNameSignInfo)"
    FileSignInfo="@(FileSignInfo)"
    TempDir="$(ArtifactsTmpDir)"
    LogDir="$(ArtifactsLogDir)"
    PublishUrl="$(PublishUrl)"
    OrchestrationManifestPath="$(OrchestrationManifestPath)"
    MSBuildPath="$(DesktopMSBuildPath)"
    MicroBuildCorePath="$(NuGetPackageRoot)microbuild.core\$(MicroBuildCoreVersion)"/>
...
```

## Valid Argument Values

**Certificate Name:** name of the Authenticode certificate to use for signing.  Valid values include `Microsoft402`, `WindowsPhone623`, `MicrosoftSHA1Win8WinBlue` and `VsixSHA2`.  

**Strong Name:** name of the key to use when strong naming the binary. This can be `null` for values which do not require strong name signing such as VSIX files. 

**Target Framework:** valid values include (but are not limited to): `.NETStandard,Version=v2.0`,  `.NETFramework,Version=v4.6.1`, `.NET Core,Version=v2.0`, etc.
