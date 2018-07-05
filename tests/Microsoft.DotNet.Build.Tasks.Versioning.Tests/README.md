# How to Test the MSBuild Versioning Targets

1. Create a new [.Net Core] library project.
2. Add the Versioning NuGet package as a PackageReference. You might need to setup a local repo.
3. Build the library. 

Inspect the library file (DLL) and check if the fields (File Version, Product Version, Assembly Version) match what you expected.

4. Pack the library to create a NuGet package.

Check that the name of the package and the contents of the .nuspec file contains the correct version information.

