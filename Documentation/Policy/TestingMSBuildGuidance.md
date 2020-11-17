# Guidance for Testing MSBuild functionality

In an effort to improve the ability to test and validate MSBuild functionality, please follow this guidance when working in those areas: 

- When possible move as much logic out of MSBuild proj files and into the C# tasks (or related classes) so that it can be unit tested.
- Use mocks and dependency injection where applicable when writing unit tests for your feature code. If the code does not have an entry point to impelement the ASP.NET Core Dependency Injection framework, use the Setter Injection pattern. 
- Implement scenario tests for MSBuild projects in Arcade Validation. Validate your changes by [running a private build](#how-to-validate-a-private-build) of Arcade in Arcade Validation. Include a link of the Arcade Validation run on your Arcade PR. 

## How to Validate a Private Build

1. Run a build of your Arcade branch on the [arcade-official-ci](https://dnceng.visualstudio.com/internal/_build?definitionId=6) Azure DevOps Pipeline
2. Promote your build to the "General Testing" Maestro channel. 
3. Create a branch of [Arcade Validation](https://github.com/dotnet/arcade-validation)
4. Using darc, run `update-dependencies` on your Arcade Validation branch to use the build of Arcade you just created in the previous steps. 
5. Run a build of your Arcade Validation branch on the [dotnet-arcade-valdiation-official](https://dnceng.visualstudio.com/internal/_build?definitionId=282) to verify your changes. 
6. It's not necessary to merge your Arcade Validation branch into the repo's main branch, so feel free to delete it when you're done validating your changes. 

Notes: 
- We are currently investigating how to unit tests MSBuild projects. 
