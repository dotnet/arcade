# DeltaBuild

DeltaBuild is build tool for code bases that have many projects. By analyzing dependencies and Git history, DeltaBuild can determine subset of projects that need to be rebuilt and by doing so substantially decrease build time. This is especially useful on PR gates. Result are not bullet-proof, they are best-effort. Contributions are welcomed.

## How it works

DeltaBuild relies on information provided by MSBuild and Git. 

1. Execute `dotnet restore /bl` to dump MSBuild's binary log from your feature branch and pass it to DeltaBuild.
1. DeltaBuild performs `git diff` between your feature branch and target branch (default: main).
1. DeltaBuild creates cross-section between information contained in the binary log and git difference.

To cover cases when projects are deleted, you need to additionally execute `dotnet restore /bl` for target branch.

DeltaBuild can produce a JSON file with list of projects that need to be considered for rebuilding.


Example output for a small change.
```
Running DeltaBuild. This may take some time.
Binary log: C:\__w\1\s\msbuild.binlog
Repository path: C:\__w\1\s
Changed file: C:\__w\1\s\src\My.Component\My.Component.csproj
{
  "AffectedProjectChain": [
    "C:\\__w\\1\\s\\src\\\My.Component\\My.Component.csproj",
    "C:\\__w\\1\\s\\test\\\My.Component.Tests\\My.Component.Tests.csproj"
  ],
  "AffectedProjects": [
    "C:\\__w\\1\\s\\src\\Analyzers\\Aalyzers.csproj",
    "C:\\__w\\1\\s\\src\\\My.Component\\My.Component.csproj",
    "C:\\__w\\1\\s\\test\\\My.Component.Tests\\My.Component.Tests.csproj"
  ]
}
```

In this example, a change to `src\My.Component\My.Component.csproj` means we need to rebuild project, its test project, and analyzer that is a transitive dependency.

## How to run

To run DeltaBuild, you need a binary log and path to the repository:
```
dotnet-deltabuild --binlog msbuild.binlog --repository C:\_w\1\s
```

Optionally you may provide..
- `-d|--debug` if you'd like to see additional information.
- `-o|--output-json` to capture result in a file.
- `-bbl|--branch-binlog` to account for changes where file or project had been deleted.
- `-b|--branch` to specify target branch (default: `origin/main`)


## Results

Depending on size of the solution and relative change, measured speedup can vary from 25% to 85%.
