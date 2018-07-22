setlocal
    echo off
    set RunnerSourceCodeFolder=%1
    set DestinationFolder=%2

    REM Rebuild the runner project
    msbuild /t:rebuild "%RunnerSourceCodeFolder%\XUnit.Runner.Uap.csproj" /p:AppxPackageDir=%RunnerSourceCodeFolder%\AppPackages

    REM Unpack the appx file to a folder
    makeappx unpack /l /o /p "%RunnerSourceCodeFolder%\AppPackages\XUnit.Runner.Uap_1.0.0.0_x64_Debug_Test\XUnit.Runner.Uap_1.0.0.0_x64_Debug.appx" /d "%RunnerSourceCodeFolder%\UnpackedAppx"

    REM Copy the files we care about from the unpacked folder
    call :CopyRunnerFilesFromAppx "%RunnerSourceCodeFolder%\UnpackedAppx" %DestinationFolder%

    REM Copy the master Appxmanifest file to the clean runner since that can change
    REM The assumption is that the Appxmanifest file is next to this script
    copy /y %~dp0Appxmanifest_master.xml %DestinationFolder%\AppxManifest.xml

    echo on
    goto :EOF

:CopyRunnerFilesFromAppx 
    set _source=%~1
    set _dest=%~2

    xcopy /y %_source%\Assets\LockScreenLogo.scale-200.png %_dest%\Assets\
    xcopy /y %_source%\Assets\SplashScreen.scale-200.png %_dest%\Assets\
    xcopy /y %_source%\Assets\Square150x150Logo.scale-200.png %_dest%\Assets\
    xcopy /y %_source%\Assets\Square44x44Logo.scale-200.png %_dest%\Assets\
    xcopy /y %_source%\Assets\Square44x44Logo.targetsize-24_altform-unplated.png %_dest%\Assets\
    xcopy /y %_source%\Assets\StoreLogo.png %_dest%\Assets\
    xcopy /y %_source%\Assets\Wide310x150Logo.scale-200.png %_dest%\Assets\
    xcopy /y %_source%\clrcompression.dll %_dest%\
    xcopy /y %_source%\entrypoint\XUnit.Runner.Uap.exe %_dest%\entrypoint\
    xcopy /y %_source%\Properties\Default.rd.xml %_dest%\Properties\
    xcopy /y %_source%\resources.pri %_dest%\
    xcopy /y %_source%\System.ComponentModel.DataAnnotations.dll %_dest%\
    xcopy /y %_source%\System.Net.dll %_dest%\
    xcopy /y %_source%\System.Private.ServiceModel.dll %_dest%\
    xcopy /y %_source%\System.ServiceModel.dll %_dest%\
    xcopy /y %_source%\System.ServiceModel.Duplex.dll %_dest%\
    xcopy /y %_source%\System.ServiceModel.Http.dll %_dest%\
    xcopy /y %_source%\System.ServiceModel.NetTcp.dll %_dest%\
    xcopy /y %_source%\System.ServiceModel.Primitives.dll %_dest%\
    xcopy /y %_source%\System.ServiceModel.Security.dll %_dest%\
    xcopy /y %_source%\System.ServiceModel.Web.dll %_dest%\
    xcopy /y %_source%\System.Windows.dll %_dest%\
    xcopy /y %_source%\System.Xml.Serialization.dll %_dest%\
    xcopy /y %_source%\vs.appxrecipe %_dest%\
    xcopy /y %_source%\WinMetadata\Windows.winmd %_dest%\WinMetadata\
    xcopy /y %_source%\XUnit.Runner.Uap.exe %_dest%\
    xcopy /y %_source%\XUnit.Runner.Uap.xr.xml %_dest%\
    xcopy /y %_source%\xunit.runner.utility.dotnet.dll %_dest%\
    xcopy /y %_source%\RunnerRemoteExecutionService.winmd %_dest%\

    xcopy /y "C:\Program Files\WindowsApps\Microsoft.VCLibs.140.00_14.0.24123.0_x64__8wekyb3d8bbwe\*.dll" %_dest%\
    xcopy /y "C:\Program Files\WindowsApps\Microsoft.NET.CoreRuntime.1.1_1.1.24920.0_x64__8wekyb3d8bbwe\uaphost.dll" %_dest%\
    xcopy /y "C:\Program Files\WindowsApps\Microsoft.NET.CoreRuntime.1.1_1.1.24920.0_x64__8wekyb3d8bbwe\uwphost.dll" %_dest%\
GOTO:EOF
endlocal