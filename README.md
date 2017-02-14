# Sign Tool

This is a batch signing and verification tool for Microbuild environments.  The tool is run as a post build step and driven by a declarative configuration file.  The high level features of the tool are:

- Performance: The tool operates as a post build step and uses the minimum number of requests possible.  This can have a dramatic performance improvement over the typical implementation which signs as a post compile step.  For example, in Roslyn it took build + sign times down from 1-2 hours to 8 minutes. 
- Verification: Using the `-test` argument the tool can be run as part of a CI leg to verify the consistency of the configuration file.  This enables developers to catch many build and packing errors that normally would cause a signed build to break post check-in.
- VSIX: The tool can handle the nesting issues that come with VSIX: both nested PE and nested VSIX.  The tool will correctly sign the VSIX content first, repack the VSIX with signed content and then sign the containing VSIX.  Arbitrary levels of nesting are supported.
- Declarative config file: The config file is designed to be declartive and explicit about all files included in the signing process. 
- Post signing checks: Takes extra steps to ensure a file is properly signed after the signing process completes. 

## Configuration File

The configuration file has two main sections: sign and exclude.  

``` json
{
    "sign": [ ],
    "exclude": [ ]
}
```

Each entry in the sign section has the following format: 

``` json
{
    "certificate": "",
    "strongName": "",
    "values": [
        "file1",
        "file2"
    ]
}
```

The properties have the following semantics:

- certificate: name of the authenticode certificate to use for signing.  Valid values include Microsoft402, WindowsPhone623, MicrosoftSHA1Win8WinBlue and VsixSHA2.  More are likely available.  This value must be specified.
- strongName: name of the key to use when strong naming the binary.  This can be `null` for values which do not require strong name signing such as VSIX files. 
- values: array of paths, relative to the binaries directory, which will be signed in this manner. 

The exclude section is only relevant when VSIX values are being signed.  It's not uncommon to include files in a VSIX which are not built by the repo producing the VSIX.  Such files should not be included in signing (responsibility of the repo that produced them).  

Part of the sign tool verification process is to ensure every file is properly accounted for.  That includes digging through VSIX and making sure there are no stray entries.  The exclude list serves an explicit declaration that the file is a) meant to be in the VSIX and b) not meant to be signed.

Example configuration files:

- [Roslyn](https://github.com/dotnet/roslyn/blob/master/build/config/SignToolData.json)
- [DiaSym Reader](https://github.com/dotnet/symreader/blob/master/build/Signing/SignToolData.json)

## Arguments

The command line for SignTool is the following:

> SignTool.exe [-test] [-msbuildPath <path>] [-config <path>] [-intermediateOutputPath <path>] [-nugetPackagesPath <path>] outputPath

The only required argument is `outputPath` and `-config`.  The rest will be inferred to reasonable defaults. Detailed break down:

- `-config`: Path to the configuration file. Default is to use `build\config\SignToolData.json` from the nearest directory containing `.git`.
- `<output path>`: The base path for which all the entries in the config file are based off of. 
- `-test`: The tool will operate in verification mode.  This checks the correctness of the config file, ensures the VSIX have contents that are identical to the build output (not just name matching), and that binaries are in a correct signing state.  Designed for developer and CI runs.
- `-msbuildPath`: Path to the MSBuild.exe binary used to run the actual signing process on Microbuild.  The default is to use MSBuid 14.0 standard installation.
- `-nugetPackagesPath`: Defaults to `~\.nuget\packages`.  Needed to specify the `<Import>` statements for Microbuild.
- `-intermediateOutputPath`: Defaults to `<outputPath>\Obj`.  
