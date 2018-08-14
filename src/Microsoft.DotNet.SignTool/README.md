# Sign Tool

This is a MSBuild task that provide batch signing and verification for 
Microbuild environments. The tool is run as a post-build step and is able 
to infer the files that need to be signed given a list of container files 
(.nupkg, .vsix, etc) as input. The high level features of the tool are:

- Performance: The tool operates as a post-build step and uses the minimum number of requests possible. This can have a dramatic performance improvement over the typical implementation which signs as a post-compile step. For example, in Roslyn it took build + sign times down from 1-2 hours to 8 minutes. 
- Automated: This tool is able to automatically recursively open container files (like .nupkg, .vsix) and create a list of nested files that need to be signed. Therefore, you don't need to manually specify which files need to be signed.
- Verification: Using the `-test` argument the tool can be run as part of a CI leg to verify the consistency of the input configuration. This enables developers to catch many build and packaging errors that normally would cause a signed build to break post check-in.
- VSIX: The tool can handle the nesting issues that come with VSIX: both nested PE and nested VSIX.  The tool will correctly sign the VSIX content first, repack the VSIX with signed content and then sign the containing VSIX. Arbitrary levels of nesting are supported.
- Post signing checks: Takes extra steps to ensure a file is properly signed after the signing process completes. 

## Arguments

- `DryRun`: 
- `TestSign`: The binaries will be test signed. The default is to real sign.
- `ItemsToSign`: 
- `StrongNameSignInfo`: 
- `FileSignInfo`: 
- `MicroBuildCorePath`: 
- `MSBuildPath`: Path to the MSBuild.exe binary used to run the actual signing process on Microbuild.  The default is to use MSBuid 14.0 standard installation.
- `TempDir`: 
- `LogDir`: 
- `PublishUrl`: 

- `-test`: The tool will operate in verification mode.  This checks the correctness of the config file, ensures the VSIXes have contents that are identical to the build output (not just name matching), and that binaries are in a correct signing state.  Designed for developer and CI runs.




## Configuration File

The properties have the following semantics:

- certificate: name of the Authenticode certificate to use for signing.  Valid values include Microsoft402, WindowsPhone623, MicrosoftSHA1Win8WinBlue and VsixSHA2.  More are likely available.  This value must be specified.
- strongName: name of the key to use when strong naming the binary.  This can be `null` for values which do not require strong name signing such as VSIX files. 
- values: array of paths, relative to the binaries directory, which will be signed in this manner.  These paths can include `*` globbing for directory names (helps support localization). 



Part of the sign tool verification process is to ensure every file is properly accounted for.  
That includes digging through VSIX and making sure there are no stray entries.  The exclude list 
serves as an explicit declaration that the file is a) meant to be in the VSIX and b) not meant 
to be signed.



- `-test`: The tool will operate in verification mode.  This checks the correctness of the config file, ensures the VSIXes have contents that are identical to the build output (not just name matching), and that binaries are in a correct signing state.  Designed for developer and CI runs.
- `-testSign`: The binaries will be test signed. The default is to real sign.

