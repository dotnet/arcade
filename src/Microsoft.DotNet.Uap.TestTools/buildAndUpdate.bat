setlocal
    echo off
    set RunnerSourceFolder=%1
    set LauncherSourceFolder=%2
    set XUnitConsoleRunnerFolder=%3
    set DestinationFolder=%4
    
    call :LauncherMain x86
    call :RunnerMain x86

    call :LauncherMain x64
    call :RunnerMain x64

    call :LauncherMain arm
    call :RunnerMain arm

    call :LauncherMain arm64
    call :RunnerMain arm64

    echo on
    goto :EOF

:LauncherMain
    set _platform=%~1

    REM Rebuild the launcher project
    msbuild /t:rebuild "%LauncherSourceFolder%\WindowsStoreAppLauncher.vcxproj" /p:Platform=%_platform% /p:Configuration=Release

    REM Copy the launcher executable
    xcopy /y "%LauncherSourceFolder%\bin\%_platform%\Release\WindowsStoreAppLauncher.exe" "%DestinationFolder%\Tools\%_platform%\Launcher\"
    GOTO :EOF

:RunnerMain
    set _platform=%~1

    REM Rebuild the runner project
    msbuild /t:rebuild "%RunnerSourceFolder%\Microsoft.DotNet.XUnitRunnerUap.csproj" /restore /p:Platform=%_platform% /p:AppxPackageDir=bin\AppPackages
    
    REM Unpack the main appx
    makeappx unpack /l /o /p "%RunnerSourceFolder%\bin\AppPackages\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug_Test\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug.appx" /d "%RunnerSourceFolder%\bin\unpack\Microsoft.DotNet.XUnitRunnerUap\%_platform%"
    
    REM Unpack dependency appx
    IF "%_platform%" == "arm64" (
        makeappx unpack /l /o /p "%RunnerSourceFolder%\bin\AppPackages\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug_Test\Dependencies\%_platform%\Microsoft.VCLibs.%_platform%.14.00.appx" /d "%RunnerSourceFolder%\bin\unpack\Microsoft.VCLibs.%_platform%.Debug.14.00\%_platform%"
    ) ELSE (
        makeappx unpack /l /o /p "%RunnerSourceFolder%\bin\AppPackages\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug_Test\Dependencies\%_platform%\Microsoft.NET.CoreRuntime.2.2.appx" /d "%RunnerSourceFolder%\bin\unpack\Microsoft.NET.CoreRuntime.2.2\%_platform%"
        makeappx unpack /l /o /p "%RunnerSourceFolder%\bin\AppPackages\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug_Test\Dependencies\%_platform%\Microsoft.NET.CoreFramework.Debug.2.2.appx" /d "%RunnerSourceFolder%\bin\unpack\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%"
        makeappx unpack /l /o /p "%RunnerSourceFolder%\bin\AppPackages\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug_Test\Dependencies\%_platform%\Microsoft.VCLibs.%_platform%.Debug.14.00.appx" /d "%RunnerSourceFolder%\bin\unpack\Microsoft.VCLibs.%_platform%.Debug.14.00\%_platform%"
    )

    REM Copy the files we care about from the unpacked folder
    call :RunnerCopy "%RunnerSourceFolder%\bin\unpack" "%DestinationFolder%\Tools\%_platform%\Runner"
    GOTO :EOF

:RunnerCopy
    set _source=%~1
    set _dest=%~2
    set _mainSource=%~1\Microsoft.DotNet.XUnitRunnerUap\%_platform%

    xcopy /y %_mainSource%\Assets\LockScreenLogo.png %_dest%\Assets\
    xcopy /y %_mainSource%\Assets\SplashScreen.png %_dest%\Assets\
    xcopy /y %_mainSource%\Assets\Square150x150Logo.png %_dest%\Assets\
    xcopy /y %_mainSource%\Assets\Square44x44Logo.png %_dest%\Assets\
    xcopy /y %_mainSource%\Assets\Square71x71Logo.png %_dest%\Assets\
    xcopy /y %_mainSource%\Assets\StoreLogo.png %_dest%\Assets\
    xcopy /y %_mainSource%\Assets\Square310x310Logo.png %_dest%\Assets\
    xcopy /y %_mainSource%\Assets\Wide310x150Logo.png %_dest%\Assets\
    xcopy /y %_mainSource%\Microsoft.DotNet.XUnitRunnerUap.exe %_dest%\
    xcopy /y %_mainSource%\resources.pri %_dest%\
    xcopy /y .\AppxManifest.xml %_dest%\
    powershell -Command "(gc %_dest%\AppxManifest.xml) -replace '\$processorArchitecture\$', '%_platform%' | Out-File -encoding ASCII %_dest%\AppxManifest.xml"
    xcopy /y "%_source%\Microsoft.VCLibs.%_platform%.Debug.14.00\%_platform%\*.dll" "%_dest%\"    

    IF "%_platform%" == "arm64" (
        xcopy /y %_mainSource%\clrcompression.dll %_dest%\
        xcopy /y %_mainSource%\Microsoft.DotNet.XUnitRunnerUap.dll %_dest%\
    ) ELSE (
        xcopy /y %_mainSource%\entrypoint\Microsoft.DotNet.XUnitRunnerUap.exe %_dest%\entrypoint\
        xcopy /y %_mainSource%\Properties\Default.rd.xml %_dest%\Properties\
        xcopy /y %_mainSource%\WinMetadata\Windows.winmd %_dest%\WinMetadata\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.Private.ServiceModel.dll" %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.ServiceModel.dll" %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.ServiceModel.Duplex.dll" %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.ServiceModel.Http.dll" %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.ServiceModel.NetTcp.dll" %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.ServiceModel.Primitives.dll" %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.ServiceModel.Security.dll" %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.ServiceModel.Web.dll" %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreFramework.Debug.2.2\%_platform%\System.ServiceProcess.dll" %_dest%\
        xcopy /y %_mainSource%\xunit.abstractions.dll %_dest%\
        xcopy /y %_mainSource%\xunit.assert.dll %_dest%\
        xcopy /y %_mainSource%\xunit.core.dll %_dest%\
        xcopy /y %_mainSource%\xunit.execution.dotnet.dll %_dest%\
        xcopy /y %_mainSource%\xunit.runner.utility.uwp10.dll %_dest%\
        xcopy /y "%_source%\Microsoft.NET.CoreRuntime.2.2\%_platform%\uwphost.dll" "%_dest%\"
    )

GOTO:EOF
endlocal