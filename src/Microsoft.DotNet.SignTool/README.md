# Sign Tool

This is a MSBuild task that provide batch signing and verification for 
Microbuild environments. The tool is run as a post-build step and is able 
to infer the files that need to be signed given a list of container files 
(.nupkg, .vsix, etc) as input. The high level features of the tool are:

- Performance: The tool operates as a post-build step and uses the minimum 
number of requests possible. This can have a dramatic performance improvement 
over the typical implementation which signs as a post-compile step. For example, 
in Roslyn it took build + sign times down from 1-2 hours to 8 minutes. 

- Automated: This tool is able to automatically recursively open container files 
(like .nupkg, .vsix) and create a list of nested files that need to be signed. 
Therefore, you don't need to manually specify which files need to be signed.

- Containers: The tool can handle the nesting issues that come with container 
files: both nested PE and further nested containers. The tool will correctly 
sign the inner most content first, repack the containing package with the signed 
content and then sign the outermost container. Arbitrary levels of nesting 
are supported.

- Post signing checks: Takes extra steps to ensure a file is properly signed 
after the signing process completes. 

- Manifest generation: By specifying the path to store a manifest file you are 
able to see the list of files that would be signed and their respective signing
information (Certificate + Strong Name).

## Arguments

- `DryRun`: Perform validation but do not actually send signing request to the server.
- `TestSign`: The binaries will be test signed. The default is to real sign.
- `ItemsToSign`: This is a list of full path to files that need to be signed. Container files will be expanded to look for further files to be signed.
- `StrongNameSignInfo`: Used to pass the the default certificate and strong name to be used for a given Public Key Token. See details below.
- `FileSignInfo`: Used to convey overriding information for specific files and target frameworks. See details below.
- `MicroBuildCorePath`: Path to MicroBuild.Core package directory.
- `MSBuildPath`: Path to the MSBuild.exe binary used to run the actual signing process on Microbuild.  The default is to use MSBuid 14.0 standard installation.
- `PublishUrl`: The URL of the feed where the package will be published. This is not used anywhere for sining the package, only as complimentary information for the orchestration manifest file.
- `OrchestrationManifestPath`: Path (including file name) to where to store a manifest file containing the list of files that would be signed and their respective signing information.
- `TempDir`: Used to store temporary files used during the process of calling Microbuild signing.
- `LogDir`: MSBuild log information from the sining rounds will be stored in this directory.

# Arguments Metadata

*StrongNameSignInfo* 

This field assumes the following metadata/properties: `PublicKeyToken`, 
`CertificateName` and the `Include` field is assumed to hold the StrongName. 
This information will be used as the default certificate and strong name 
information for all files that match the PublicKeyToken.

*FileSignInfo* 

This field assumes the following metadata: `PublicKeyToken`, `CertificateName`, 
`TargetFramework` and the `Include` field is assumed to hold a file name 
(including extension; not a full path). and so on. More over it 
also accepts the "All" sentinel to denote any target framework.

The `CertificateName` attribute might also contain the value "None" to flag a file
that should not be signed.

All files that are required to be signed and match the combination of (File Name, 
Public Key Token, Target Framework) will use overriding Certificate Name informed.


## Example Usage

- Certificate Name: name of the Authenticode certificate to use for signing.  
Valid values include Microsoft402, WindowsPhone623, MicrosoftSHA1Win8WinBlue 
and VsixSHA2.  More are likely available.  This value must be specified.

- Strong Name: name of the key to use when strong naming the binary. This 
can be `null` for values which do not require strong name signing such as VSIX files. 

- Target Framework: `TargetFramework` field are: `.NETStandard,Version=v2.0`, 
`.NETFramework,Version=v4.6.1`, `.NET Core,Version=v2.0`, etc.

