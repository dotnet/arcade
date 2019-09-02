# Toolset Principles and Guidance

For purposes of this document, 'Toolset' refers to tools which create and/or modify the shipping product. 

## Principles

- **Consistent across the stack:** The minimum number of tool versions should be used - with the ideal being one version per tool.  
- **Visible:** It must be obvious which tool is being used in which situation.
- **For tools MSFT owns, the latest shipping version is used to build .NET Core:** Our product/s should be built using the shipping version of our toolsets.

## Policy / Guidance

### General
- Which tool version used across the stack should be determined in one place.  For more comments on this, see farther below.
- The latest version of a tool should be used.  This implies ongoing diligence to update our toolset dependencies.
- Tools should be bootstrapped into the build where ever possible.  It is recognized that this is not always reasonable (or even the best approach), but is still desireable as we try and get as close as possible to 'clone and build'.

### VC toolset
- VC tools are brought in via Visual Studio, preferably via the build sku.  Given the install limitations of VS and the Windows SDK, VC tools are provided via VM images which make up our build/test pools.   
- Tools from the latest public preview of VS are available via a machine pool.  Private previews of VS should not be widely available and used for targeting testing only.
- The guidance is to do limited building and testing on the preview versions of VS, such that the upgrade to the latest tools can be done with confidence.  The distinction here comes from two competing business priorities: 1) Always be building and testing using the latest VC toolset, and 2) Keep our cost at a reasonable level. The RTM'd version of VS is on the hosted pools, while previews are on our private pools.

### Managed toolset
- Roslyn version is brought in as a dependency via the Arcade SDK.  It is not recommended for any build to take a direct dependency on Roslyn
- In cases where an older version of .NET (and by implication Roslyn) is needed, exceptions are supported via Arcade.  

### Linux native toolsets
- The native *nix build tools are managed via docker containers.
- Support for "blessed" containers that can be shared across the teams is preferable.

### Mac native toolsets
- Mac tools are managed via O/S itself.  Here too we have both hosted and private machine pools.

### Other
- Any other tools not directly called out should be managed via Arcade, and preferably bootstrapped in as part of the build.