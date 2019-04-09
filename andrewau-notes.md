# Goal
The goal of this branch is to fix issue  [#2287](https://github.com/dotnet/arcade/issues/2287)

# Where is the script generator
[Here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.CoreFxTesting/build/assets/RunnerTemplate.Unix.txt) is the template that generate the script.

[Here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.CoreFxTesting/build/assets/RunnerTemplate.Windows.txt) is the one for Windows

# How to parse named argument in bash
The usual approach, let's find something to copy from, [this](https://github.com/dotnet/arcade/blob/d3b40a5a2cbb2e00503413b260fe5c49ce3c2691/eng/common/build.sh#L77) one seems relevant.

Checkout my test.sh for a prototype required named argument parser.

Just in case we wanted more bash programming that is not available to copy, [here](https://en.wikibooks.org/wiki/Bash_Shell_Scripting) is probably a fine reference.

# How to parse named argument in cmd
The usual approach, let's find something to copy from, [This](https://github.com/dotnet/arcade/blob/master/eng/common/CIBuild.cmd) one seems useless, almost all cmd file we have simply delegate to powershell. That makes me wonder, shall we do that too? It incurs the cost of launching powershell and writing a new script in powershell syntax, probably not worth it just for the command line parser.

Without a good sample, I am experimenting with writing my own. This seems to be a [fine](https://stackoverflow.com/questions/4094699/how-does-the-windows-command-interpreter-cmd-exe-parse-scripts) reference for the various command line argument magic. [This](https://en.wikibooks.org/wiki/Windows_Batch_Scripting) is more useful as it provides primitives to use to do programming.

Checkout my test.cmd for a prototype required named argument parser.

# How to modify the script generator
The script templates are updated.

# How to test?
With help from @hoyosjs, we figured this is the procedure for testing the template generator:

This command will exercise the template generator:
```
build.cmd -restore -build -configuration Debug -ci -buildtests -arch x64 -framework netcoreapp /p:ArchiveTests=Tests /p:OuterLoop=false /p:RuntimeOS=win10 -bl
```
Running this command is pretty slow, but at least it solved the problem of exercising the template generator.

Once the command completes, we can get to the build log:
```
C:\Dev\corefx\artifacts\log\Debug\Build.binlog
```

The build log can be opened using the [structured MSBuild Log viewer](http://www.msbuildlog.com/), search for the GenerateRunScript and we will find out where the task takes the template from and where does it generate output to:
```
$task GenerateRunScript
```

Expand the Task, the parameters told us where does it obtain the script template from and the script file it will write to:

Now I modified the template and run the build again (there is no need to clean or delete or anything, it appears that the template is re-generated regardless (as of commit 6ca16758a5d454c1f1b04975bf55f259dd71fc49)).

```
TemplatePath = C:\Dev\corefx\.packages\microsoft.dotnet.corefxtesting\1.0.0-beta.19205.6\build\assets\RunnerTemplate.Windows.txt
OutputPath = C:\Dev\corefx\artifacts\bin\tests\System.Console.Manual.Tests\netcoreapp-Windows_NT-Debug-x64\RunTests.cmd
```

Now we can test if the generated script look the way I want it to be.

# How to change script callers
Thanks to @ViktorHofer and @hoyosjs, we have a pretty good idea where the callers are, and we will simply make breaking change to the script interface, expecting when the change is consumed, the appropriate callers are patched.