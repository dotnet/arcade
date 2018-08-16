# Microsoft.DotNet.Uap.TestTools

This folder contains a custom xunit UWP Console App runner `Microsoft.DotNet.XUnitRunnerUap` and a support application `WindowsStoreAppLauncher` which deals with ingesting and packaging assemblies into the runner appx package as dynamic assembly loading is currently not supported in UWP.

## Microsoft.DotNet.XUnitRunnerUap

A UWP console app which executes xunit inside to invoke tests. Designed to support the same parameters as the official xunit runners.

Requirements to run:
 - Windows 10 >= v10.0.17134 (April 2018 Update)

Supported platforms:
 - x64
 - x86
 - ARM

 To build and bundle the runner for deployment invoke the `buildAndUpdateRunner.bat` script as following: `.\buildAndUpdateRunner.bat src out`. This will create an output directory (second parameter) which can be copied into the existing nuget package `Microsoft.DotNet.Uap.TestTools` for updates.

 ## Launcher

 A native application written in C++ which has the following responsibilities:
 - Bundles the runner and the to be invoked test assembly together as one appx
 - Registers the appx
 - Unregister the appx

 Please invoke the application to see all possible arguments.