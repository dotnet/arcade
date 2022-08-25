Microsoft.DotNet.GenAPI
===============================

Contains GenAPITask - MSBuild task that allows to synthesize C# source tree out of a dll.
The project is build on top of Microsoft.CCI and Microsoft.CCI.Extensions.

Currently, new Roslyn-based GenAPIv2 is in development in folders:
* Shared - common library for Tool and Tasks.
* Tasks - MSBuild tasks for GenAPIv2.
* Tool - CLI tool used to generate reference assemblies out of executable and produce output in console or to file.

Supported parameters in GenAPIv2:
* **Assembly** - [required] Path for an specific assembly or a directory to get all assemblies.
* **GenAPILibPath** - Delimited (',' or ';') set of paths to use for resolving assembly references.
* **ApiList** - Specify a api list in the DocId format of which APIs to include.
* **ExcludeApiList** - Specify a api list in the DocId format of which APIs to exclude.
* **OutputPath** - Output path. Default is the console. Can specify an existing directory as well and then a file will be created for each assembly with the matching name of the assembly
* **HeaderFile** - Specify a file with an alternate header content to prepend to output.
* **ExceptionMessage** - Method bodies should throw PlatformNotSupportedException.
* **ExcludeAttributesList** - Specify a list in the DocId format of which attributes should be excluded from being applied on apis.
* **FollowTypeForwards** - Resolve type forwards and include its members.

We'll remove the original CCI-based implementation after GenAPIv2 is ready.