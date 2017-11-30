# AppCompat private runs

This page describes how to use the AppCompat infrastructure to validate a private build or feature of .NET Native. Private runs use the exact same infrastructure as the official runs as described in [AppCompat Runs in Helix](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/appcompat-helix-operations.md). Please note that this functionality is still in development, so things will change. If you have any feedback whatsoever please contact [corefxappcompat](corefxappcompat@microsoft.com).

## Submitting a private run
From any command line:  
`> \\ac-rc-ddit\PrivateRuns\tools\StartAppCompatPrivateRun.exe --testIlcPath <Path to TestILC>`

This will submit a private Top50 AppCompat run for x86 Shared Assembly (MultiFile) using the TestILC from the specified path. Additional available options are (case sensitive!):
* `--arch <x86/amd64/arm>` - the architecture of the apps to use (the specified TestILC should target this architecture). Default is x86.
* `--appBuildType <ret/chk>` - the build type to build the apps with. Default is ret.
* `--appCount <50/500/2000>` - the number of apps to run, the Top#. Default is 50.  
 Please be considerate of how many apps you really need to run. Large runs are relatively expensive.
* `--singleFile` - will perform a Single-File compilation of the apps. Default is Multi-File/ShAsm.
* `--worklist <file path>` - path to the text file which contain the list of apps to run. The list must contain the 'old' file share paths to the apps. Default is to use the Top# app list as per `--appCount`. Note that this options overrides `--appCount`.

All paths to files and folders can be local or remote, the tool will copy their content using the current user/machine.

## Special build modes
These options can be used to turn on some of the special build modes for ILC:
* `--incrementalBuildBehavior <behavior>` - turns on incremental build for this run. Default is to use non-incremental build. Incremental build currently requires ShAsm. The specified behavior can be:
  * `TouchMainExe` - incremental clean build is performed, then the main .exe file is touched (it's last write timestamp is modified to current time) and then a delta incremental build is done. After that the app is launched.
  * `NoDelta` - incremental clean build is performed and then immediately a delta incremental build is done (nothing is changed). After that the app is launched.
  * `CleanOnly` - only the incremental clean build is performed. After that the app is launched.
* `--tp` - the run will be a targeted patching run. Following applies:
  * Last shipped version of ILC is used to build the apps (not the one specified in the run).
  * Last shipped shared library is used to build the apps against (typically slightly different from the last shipped ILC)
  * To run the apps the shared library from the specified TestILC is used - note that by default we simply take the SharedLibrary.dll from the TestILC and run the apps with it.
* `--pureNative` - apps are built with `/pureNative` option. If shared library is to be used, it will be built (by the test infra) using the specified TestILC and with the `/pureNative` option (so the shared library in the TestILC itself is ignored).
* `--baseline` - the run will behave just like official baseline run. The specified TestILC is ignored and predetermined baseline version is used instead. If both `--tp` and `--baseline` are specified, then a different baseline build is used (the last shipped version of ILC).

For less common command line options see below.

## Private run lifetime
Running the tool will prepare the necessary assets and copy them to a share. Only users which have access to that share (`\\ac-rc-ddit\PrivateRuns`) can submit private runs. After this the tool exits.

From that point on the private run is managed by the controller machine (just like any other AppCompat run). That machine will eventually process the run and actually start it on the Helix infrastructure. Once this happens it will send an email to the user who submitted the run. Typically this email should arrive in cca 10 minutes after running the tool above.

After that the run is queued in Helix and depending on the load (private runs have their own queue) and the number of apps the run will take anywhere between couple of minutes to several hours to finish. Once it's finished the controller machine will send another email to the user with the full report. The email is nearly identical in format and content to the official run emails.

## Less common options
* `--sharedAssemblyBuild <enable/disable>` - explicitly enables or disables building of the shared library using the specified TestILC. This option only applies to ShAsm runs. Default behavior depends on the target branch and type of the run (baseline runs enable this, target runs disable it).
  * `disabled` - the shared library will be taken from the TestILC itself (in the `ExternalFiles\SharedAssemblyPath`). Make sure that the correct shared library (up-to-date) is present in the input TestILC!
  * `enabled` - before building any of the apps, the input TestILC will be used to build the shared library. With this any shared library present in the input TestILC is ignored.
* `--customSharedAssemblyPath <directory path>` - path to the shared assembly to use. If this is used, than the `--sharedAssemblyBuild` is ignored and the shared library from the specified path is used. This path should point to the shared library root, so the folder which contains the `ret` or `chk` subfolders. This option only applies to ShAsm runs. Default is none.
* `--appBuildExtraArgs` - extra arguments to pass to ILC when building the apps.
* `--sharedAssemblyBuildExtraArgs` - extra arguments to pass to ILC when building the shared library. Only used if shared library build is enabled via the `--sharedAssemblyBuild` option.
* `--runName` - custom name of the run to use.
* `--disableTrimming` - disables trimming of the TestILC and potentially other input. By default, the infra will remove certain parts of the TestILC directory tree to save space and make the runs faster. This can break certain special runs and/or configurations, so use this option to disable it if necessary.

