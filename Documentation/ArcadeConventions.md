# Arcade Conventions

These are the conventions used in the Arcade repository.

### Naming of Scripts

File names and file extensions should be lowercase. For instance: `build.cmd`, `build.sh`, `restore.cmd`, etc.

### Dependent Packages Version

Package versions are stored in properties on the `eng\Versions.props` file. Use those properties to include the correct version of the package. New properties will be included as needed.

If your project depend on a package which is also part of the .NET SDK used by Arcade (check `global.json`) the project should use the version of the package available in the SDK. Otherwise, the latest stable version of the package should be used.






