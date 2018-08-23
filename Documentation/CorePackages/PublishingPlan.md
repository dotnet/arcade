# Publishing package

Currently the majority of the [tier 1 repositories](..\TierOneRepos.md) that compose .Net Core 3.0 publish:
- Shipping packages to NuGet Gallery.
**Note:** Release publishing will not be covered in Arcade (publishing to NuGet).
- Packages to MyGet and Azure blob feeds (public and private blob feed).
- Blob (MSI, zip, Linux packages).
- Linux packages to a package repository.
- Symbols to SymWeb and MSDL.

## Requirements
- Standardize the way to publish packages and symbols to the respective channels for all .NET Core 3.0 repositories.
- Publish packages to internal sources to increase reliability in our internal build systems.
- Validate the consistency of packages between the Azure Blob feeds and MyGet.
- Validate that built symbols packages are published for each package published in MyGet and Azure blob feeds.

## Roadmap
Work that will happen for S141 (09/21) is under the Epic [Publishing package](https://github.com/dotnet/arcade/issues/501) and will include:
- Remove the dependency on VersionTools.
- Inform Darc/Maestro about the things that were published and to where.
- Support publishing to MyGet, Azure blob feeds, and blobs.
- Support the Dev scenario for publishing when an official build doesn't exist.

Work after S141 (09/21) is under the Epic [Publish core "set" of shared packages](https://github.com/dotnet/arcade/issues/46):
- Add validation for consistency between MyGet and Azure blob feeds.
- Add validation to verify symbol packages upload completed.
- Ask BAR for a storage account and a key for where to publish build assets.
