# Goal
The goal of this branch is to fix issue  [#2287](https://github.com/dotnet/arcade/issues/2287)

# Subproblems
0. Where is the script generator
1. How to parse named argument in bash
2. How to parse named argument in cmd
3. How to modify the script generator
4. How to test?
5. How to change script callers

# Subproblem 0
[Here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.CoreFxTesting/build/assets/RunnerTemplate.Unix.txt) is the template that generate the script.

[Here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.CoreFxTesting/build/assets/RunnerTemplate.Windows.txt) is the one for Windows

# Subproblem 1
The usual approach, let's find something to copy from, [this](https://github.com/dotnet/arcade/blob/d3b40a5a2cbb2e00503413b260fe5c49ce3c2691/eng/common/build.sh#L77) one seems relevant.

Checkout my test.sh for a prototype required named argument parser.

Just in case we wanted more bash programming that is not available to copy, [here](https://en.wikibooks.org/wiki/Bash_Shell_Scripting) is probably a fine reference.

# Subproblem 2
The usual approach, let's find something to copy from, [This](https://github.com/dotnet/arcade/blob/master/eng/common/CIBuild.cmd) one seems useless, almost all cmd file we have simply delegate to powershell. That makes me wonder, shall we do that too? It incurs the cost of launching powershell and writing a new script in powershell syntax, probably not worth it just for the command line parser.

Without a good sample, I am experimenting with writing my own. This seems to be a [fine](https://stackoverflow.com/questions/4094699/how-does-the-windows-command-interpreter-cmd-exe-parse-scripts) reference for the various command line argument magic. [This](https://en.wikibooks.org/wiki/Windows_Batch_Scripting) is more useful as it provides primitives to use to do programming.

Checkout my test.cmd for a prototype required named argument parser.

# Subproblem 3
The command line parser is likely to be a bulky piece of code. The language sort of force us to duplicate code for each argument. It would be nice we get it right and then we engineer it into the template. It should be a simple copy and paste into the template. Depending on subproblem 5, we might have to 

# Subproblem 4
This is hard, I don't know. I am able to unit test the test.sh/test.cmd myself, but once it integrated into the template then I don't know. The `test.sh` / `test.cmd` should be a pretty comprehensive test suite for the argument parser.

# Subproblem 5
This is also hard for me, I have no idea who called this script. Once my change is in - existing caller will fail - that's not good. There might be multiple options how do we proceed:
1. Generate a different script and let individual callers opt-in, once all callers opt-in we can phase out the old script gen, this is the safest way IMHO.
2. Make the arg parser backward compatible, if the first argument is not starting with '-', it is probably the old caller and just leave the old logic around, this is just maintainence overhead.
3. Actually enumerate all the callers the best we could, and leave the rest (if any) to "if crash, we will fix it"

(1) would be my preferred choice, if we have confidence that (3) is feasible, then do (3) to avoid changes to generator.