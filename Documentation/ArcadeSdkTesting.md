# Test Support in Arcade.Sdk
Arcade.Sdk supports building test projects and running them as part of the build. XUnit, NUnit, and MSTest are supported.

## Marking Test Projects
Arcade.Sdk uses the project name to distinguish between normal projects and test projects.
Projects ending with `.PerformanceTests` are performance tests,
projects ending with `.IntegrationTests` are integration tests,
and projects ending with either `.UnitTests` or `.Tests` are unit test projects.
IntegrationTest projects are excluded from Visual Studio's Live Unit Testing, all other test projects run as normal there.

## Configuring Test Projects
All test projects can set the `TestRunnerName` property to `XUnit` (the default), `NUnit`, or `MSTest`.
This property controls what `PackageReferences` are added and what runner is used. `XUnit` by default uses the `XUnit` console runner,
but the VSTest runner can be used by setting `UseVSTestRunner` to `true`. `NUnit` and `MSTest` use the VSTest runner.
The `XUnit` console runner is no-longer supported, and will be phased out eventually. The default runner for `XUnit` will change to VSTest in the future.
`TestRunnerAdditionalArguments` can be set to add additional parameters to the runner command.
If the VSTest runner is used the `VSTestRunSettingsFile` property can be set to a `.runsettings` file that will be used.

## Running Tests
All unit tests are ran with the `Test` msbuild target. This is run through `eng\common\cibuild.ps1` or the `-test` parameter on `eng\common\build.ps1`.
Integration tests and performance tests are ran with the `IntegrationTest` and `PerformanceTest` targets.
They each have corresponding parameters `-integrationTest` and `-performanceTest` on the `eng\common\build.ps1` script.
