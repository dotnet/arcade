setlocal
    echo off
    set RunnerSourceCodeFolder=%1
    set DestinationFolder=%2

    call :RunnerMain x64
    call :RunnerMain x86
    call :RunnerMain arm

    echo on
    goto :EOF

:RunnerMain
    set _platform=%~1

    REM Rebuild the runner project
    msbuild /t:rebuild "%RunnerSourceCodeFolder%\Microsoft.DotNet.XUnitRunnerUap.csproj" /p:Platform=%_platform% /p:AppxPackageDir=bin\AppPackages
    
    REM Unpack the main appx
    makeappx unpack /l /o /p "%RunnerSourceCodeFolder%\bin\AppPackages\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug_Test\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug.appx" /d "%RunnerSourceCodeFolder%\bin\unpack\Microsoft.DotNet.XUnitRunnerUap\%_platform%"
    
    REM Unpack dependency appx
    makeappx unpack /l /o /p "%RunnerSourceCodeFolder%\bin\AppPackages\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug_Test\Dependencies\%_platform%\Microsoft.NET.CoreRuntime.2.1.appx" /d "%RunnerSourceCodeFolder%\bin\unpack\Microsoft.NET.CoreRuntime.2.1\%_platform%"
    makeappx unpack /l /o /p "%RunnerSourceCodeFolder%\bin\AppPackages\Microsoft.DotNet.XUnitRunnerUap_1.0.0.0_%_platform%_Debug_Test\Dependencies\%_platform%\Microsoft.VCLibs.%_platform%.Debug.14.00.appx" /d "%RunnerSourceCodeFolder%\bin\unpack\Microsoft.VCLibs.%_platform%.Debug.14.00\%_platform%"

    REM Copy the files we care about from the unpacked folder
    call :RunnerCopy "%RunnerSourceCodeFolder%\bin\unpack" "%DestinationFolder%\Runner\%_platform%"
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
    xcopy /y %_mainSource%\entrypoint\Microsoft.DotNet.XUnitRunnerUap.exe %_dest%\entrypoint\
    xcopy /y %_mainSource%\Properties\Default.rd.xml %_dest%\Properties\
    xcopy /y %_mainSource%\WinMetadata\Windows.winmd %_dest%\WinMetadata\
    xcopy /y %_mainSource%\Microsoft.DotNet.XUnitRunnerUap.exe %_dest%\
    xcopy /y %_mainSource%\resources.pri %_dest%\
    xcopy /y %_mainSource%\System.ServiceModel.dll %_dest%\
    xcopy /y %_mainSource%\System.ServiceModel.Duplex.dll %_dest%\
    xcopy /y %_mainSource%\System.ServiceModel.Http.dll %_dest%\
    xcopy /y %_mainSource%\System.ServiceModel.NetTcp.dll %_dest%\
    xcopy /y %_mainSource%\System.ServiceModel.Primitives.dll %_dest%\
    xcopy /y %_mainSource%\System.ServiceModel.Security.dll %_dest%\
    xcopy /y %_mainSource%\System.ServiceModel.Web.dll %_dest%\
    xcopy /y %_mainSource%\System.ServiceProcess.dll %_dest%\
    xcopy /y %_mainSource%\xunit.abstractions.dll %_dest%\
    xcopy /y %_mainSource%\xunit.assert.dll %_dest%\
    xcopy /y %_mainSource%\xunit.core.dll %_dest%\
    xcopy /y %_mainSource%\xunit.execution.dotnet.dll %_dest%\
    xcopy /y %_mainSource%\xunit.runner.utility.uwp10.dll %_dest%\
    xcopy /y .\AppxManifest.xml %_dest%\

    xcopy /y "%_source%\Microsoft.VCLibs.%_platform%.Debug.14.00\%_platform%\*.dll" "%_dest%\"    
    xcopy /y "%_source%\Microsoft.NET.CoreRuntime.2.1\%_platform%\uwphost.dll" "%_dest%\"

GOTO:EOF
endlocal