# Toolset Principles and Guidance

For purposes of this document, 'Toolset' refers to tools which create and/or modify the shipping product. 

## Principles

- **Consistent across the stack:** The minimum number of tool versions should be used - with the ideal being one version per tool for the entire .NET Core product.  
- **Visible:** It must be obvious which tool is being used in which situation.
- **For tools MSFT owns, the latest shipping version is used to build .NET Core:** Our product/s should be built using the last shipping version of our toolsets.

## Policy / Guidance

### General
- Which tool version used across the stack should be determined in one place.  For more comments on this, see farther below.
- After every shipment (previews included), all tools are brought forward to the latest appropriate release based on input from tactics.
- As needed, dependencies can be taken on non-shipping versions of a toolset so long as tactics approves.
- The latest shipping version of a tool should be used.  This implies ongoing diligence to update our toolset dependencies.
- Tools should be bootstrapped into the build where ever possible.  It is recognized that this is not always reasonable (or even the best approach), but is still desireable as we try and get as close as possible to 'clone and build'.

### VC toolset
- VC tools are brought in via Visual Studio, preferably via the build sku.  Given the install limitations of VS and the Windows SDK, VC tools are provided via VM images which make up our build/test pools.   
- Tools from the latest public preview of VS are available via a machine pool.  Private previews of VS should not be widely available and used for targeting testing only.
- The guidance is to do limited building and testing on the preview versions of VS, such that the upgrade to the latest tools can be done with confidence.  The distinction here comes from two competing business priorities: 1) Always be building and testing using the latest VC toolset, and 2) Keep our cost at a reasonable level. The RTM'd version of VS is on the hosted pools, while previews are on our private pools.

### .NET SDK / Managed toolset
- Arcade SDK (shared infra for .NET Core) must always reference the latest preview version of the .NET Core SDK at a minimum.  
- In cases where a newer version of the .NET Core SDK is needed, a non-shipping (newer) version can be referenced by the Arcade SDK with approval from tactics.
- Roslyn version is brought in as a dependency via the Arcade SDK.  It is not recommended for any build to take a direct dependency on Roslyn
- In cases where an older version of .NET (and by implication Roslyn) is needed, exceptions are supported via Arcade, but should be approved by tactics and considered highly temporary.

### Linux native toolsets
- The native \*nix build tools are managed via Docker containers.
- Where possible, the Docker containers should be shared across the teams.

### Mac native toolsets
- Mac tools are managed via O/S itself.  Here too we have both hosted and private machine pools.

### Other
- Any other tools not directly called out should be managed via Arcade, and preferably bootstrapped in as part of the build.
- All toolset updates will be communicated in advance by the engineering services team.
