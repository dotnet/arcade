// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "stdafx.h"
#include "WindowsStoreApp.h"
#include "Args.h"
#include "Support.h"
#include "dwmapi.h"

//required by CRT
extern "C" BOOL __stdcall AreFileApisANSI(void)
#pragma warning(suppress:4273) // inconsistent dll linkage
{
  return TRUE;
}
extern "C" LONG __stdcall GetCurrentPackageId(_Inout_ UINT32 *bufferLength, _Out_opt_ BYTE *buffer)
{
  UNREFERENCED_PARAMETER(bufferLength);
  UNREFERENCED_PARAMETER(buffer);
  return APPMODEL_ERROR_NO_PACKAGE;
}

using namespace std;
using namespace Microsoft::WRL;

#define DEFAULT_TIMEOUT_MILLISECONDS 1000 * 60 * 2

struct ArgInfo
{
  wstring debuggerCommandLine;
  unsigned int timeoutMilliseconds;
  bool isTimeoutSpecified;
  wstring manifestPath;
  wstring appId;
  bool isTestApp;
  bool runInBackground;
  bool installOnly;
  bool uninstallOnly;
  unsigned int delayLaunchMilliseconds;
  wstring disambiguatePrefix;
  vector<wstring> filesToDisambiguate;
  wstring executionArguments;
};

void Run(__in const ArgInfo& info,
  __out DWORD& exitCode);

int wmain(int argc, wchar_t* argv [])
{
  ArgInfo info;
  info.debuggerCommandLine = L"";
  info.timeoutMilliseconds = DEFAULT_TIMEOUT_MILLISECONDS;
  info.isTimeoutSpecified = false;
  info.manifestPath = L"";
  info.appId = L"";
  info.isTestApp = false;
  info.runInBackground = true;
  info.installOnly = false;
  info.uninstallOnly = false;
  info.delayLaunchMilliseconds = 0;
  info.disambiguatePrefix = L"";
  info.filesToDisambiguate = vector<wstring>();
  info.executionArguments = L"";
  bool help = false;

  OptionList options(L"WindowsStoreAppLauncher.exe", L"<Appx Manifest Path> [<execution args>]", L"\tAppx Manifest Path\n\t\tPath to the AppxManifest.xml file\n\texecution args\n\t\tThe arguments to pass the the application.");

  vector<wstring> argInfoTmp;

  argInfoTmp.push_back(L"?");
  argInfoTmp.push_back(L"help");
  options.Add(argInfoTmp, [&]() { help = true; }, L"Display this help");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"d");
  argInfoTmp.push_back(L"debug");
  options.Add(argInfoTmp, L"debugger cmd line", [&](const wstring& arg) { info.debuggerCommandLine = arg; }, L"Launch the application under the specified debugger");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"t");
  argInfoTmp.push_back(L"timeout");
  options.Add(argInfoTmp, L"ms", [&](const wstring& arg) { info.isTimeoutSpecified = true; info.timeoutMilliseconds = stoi(arg); }, L"The time to wait for the app to exit(default: 120000)");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"test");
  options.Add(argInfoTmp, [&]() { info.isTestApp = true; }, L"The app is a test and will exit on its own");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"foreground");
  options.Add(argInfoTmp, [&]() { info.runInBackground = false; }, L"Run the application in the foreground");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"delaylaunch");
  options.Add(argInfoTmp, L"ms", [&](const wstring& arg) { info.delayLaunchMilliseconds = stoi(arg); }, L"Delay launching the app by the specified time");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"i");
  argInfoTmp.push_back(L"install");
  options.Add(argInfoTmp, [&]() { info.installOnly = true; }, L"Install the application and quit");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"u");
  argInfoTmp.push_back(L"uninstall");
  options.Add(argInfoTmp, [&]() { info.uninstallOnly = true; }, L"Uninstall the application and quit");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"disPrefix");
  options.Add(argInfoTmp, L"name", [&](const wstring& arg) { info.disambiguatePrefix = arg; }, L"The prefix used for file disambiguation");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"disFile");
  options.Add(argInfoTmp, L"name", [&](const wstring& arg) {
    info.filesToDisambiguate.push_back(arg);
  }, L"File that is disambiguated");
  argInfoTmp.clear();
  argInfoTmp.push_back(L"appId");
  options.Add(argInfoTmp, L"id", [&](const wstring& arg) { info.appId = arg; }, L"The application ID to start");
  argInfoTmp.clear();

  wchar_t** endOfArgs = options.Parse(argc - 1, argv + 1, [&](const wstring& failedOpt)
  {
    wprintf_s(L"Error: Option %s missing argument\n", failedOpt.c_str());
    options.PrintUsage();
    exit(1);
  });

  if (argc == 1 || help)
  {
    options.PrintUsage();
    return 0;
  }

  if (endOfArgs - argv < argc)
  {
    info.manifestPath = *(endOfArgs++);
    wprintf_s(L"Got manifest file %s\n", info.manifestPath.c_str());
  }
  else
  {
    fwprintf_s(stderr, L"Error: Missing appx manifest path\n");
    options.PrintUsage();
    return 1;
  }
  while (endOfArgs - argv < argc)
  {
    info.executionArguments += wstring(L" ") + (*(endOfArgs++));
  }

  DWORD exitCode = (DWORD) -1;
  try
  {
    Run(info, exitCode);
    wprintf_s(L"\n");
    wprintf_s(L"SUCCESS\n");
    wprintf_s(L"ExitCode %d\n", exitCode);
    return exitCode;
  }
  catch (com_error e)
  {
    wprintf_s(L"\nFAILED 0x%X (%hs)\n", e.HR(), e.what());
    return 1;
  }
  catch (exception e)
  {
    wprintf_s(L"\nFAILED (%hs)\n", e.what());
    return 1;
  }
}

bool FileExists(const wstring& fname)
{
  _WIN32_FILE_ATTRIBUTE_DATA attributes;
  if (!GetFileAttributesEx(fname.c_str(), ::GET_FILEEX_INFO_LEVELS::GetFileExInfoStandard, &attributes))
  {
    DWORD error = GetLastError();

    if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND) {
      fwprintf_s(stderr, L"%s: does not exist\n", fname.c_str());
      return false;
    }
    else if (error == ERROR_INVALID_NAME)
    {
      wprintf_s(L"%s: is not a valid path\n", fname.c_str());
      return false;
    }
    else
    {
      wprintf_s(L"%s: could not be opened(%s)\n", fname.c_str(), MessageForHR(HRESULT_FROM_WIN32(error)).c_str());
      return false;
    }
  }
  return true;
}

void Install(const unique_ptr<App>& app)
{
  // Install the application
  wprintf_s(L"Removing any previous installation...\n");
  app->Remove();

  wprintf_s(L"Installing the application...\n");
  app->Add();
}

void Uninstall(const unique_ptr<App>& app)
{
  // Remove the application
  wprintf_s(L"Removing the application...\n");
  app->Remove();
}

void Execute(const unique_ptr<App>& app, const ArgInfo& info, DWORD& exitCode)
{
  try
  {
    // Enable the debugger
    // This is necessary even if no debugger is specified because otherwise starting the app can time out
    app->EnableDebug(info.debuggerCommandLine);

    if (info.delayLaunchMilliseconds > 0)
    {
      // Wait for the specified interval before launch
      // These changes allow us to avoid a race condition between app launch and the app resolver cache. 
      // See DevDiv Bug # 779425 for more details.
      wprintf_s(L"Waiting for %ims before launch...\n", info.delayLaunchMilliseconds);
      Sleep(info.delayLaunchMilliseconds);
    }

    // Start the application
    wprintf_s(L"Starting the application...\n");
    app->Start(info.runInBackground, info.isTestApp, info.executionArguments);

    bool waitResult;

    if (info.debuggerCommandLine.empty() && info.isTimeoutSpecified)
    {
        wprintf_s(L"Waiting for %ims...\n", info.timeoutMilliseconds);
        exitCode = app->WaitForExit(info.timeoutMilliseconds, &waitResult);
    }
    else
    {
        wprintf_s(L"Waiting for the application to exit...\n");
        waitResult = true;
        exitCode = app->WaitForExit();
    }

    if (!waitResult)
    {
      if (info.isTestApp)
      {
          wprintf_s(L"The app did not exit within %ims - stopping the app...\n", info.timeoutMilliseconds);
      }
      else
      {
          wprintf_s(L"Stopping the app...\n");
      }

      app->Stop();
    }

    wprintf_s(L"Disabling the debugger...\n");
    app->DisableDebug();

    if (info.isTestApp)
    {
      wprintf_s(L"\n\nSTDOUT & STDERR from immersive process:\n");
      wprintf_s(L"==================================================================================\n");
      wprintf_s(L"\n%s\n", app->GetAppStdOutContent().c_str());
      wprintf_s(L"==================================================================================\n");
    }
    else if (waitResult)
    {
      wprintf_s(L"Error: The app exited before the timeout and most likely crashed\n");
      throw com_error(E_FAIL);
    }
  }
  catch (com_error e)
  {
    wprintf_s(L"\nFAILED 0x%X (%hs)\n", e.HR(), e.what());
    Uninstall(app);
    throw;
  }
}

unique_ptr<App> CreateAppObject(__in const ArgInfo& info)
{
    return unique_ptr<App>(new WindowsStoreApp(info.manifestPath, info.appId));
}


typedef int(__stdcall *PROCDwmIsCompositionEnabled)(BOOL *pIsEnabled);
typedef int(__stdcall *PROCDwmEnableComposition)(UINT uCompositionAction);
HRESULT _DoSystemStateChecks()
{   
    HRESULT hr = S_OK;

    HINSTANCE hinstLib = LoadLibraryW(L"Dwmapi.dll");
    if (hinstLib == NULL)
    {
        wprintf_s(L"Error: Dwmapi.dll could not be found!");
        return 0x8007007E;
    }
        
    PROCDwmIsCompositionEnabled procDwmIsCompositionEnabled = (PROCDwmIsCompositionEnabled)GetProcAddress(hinstLib, "DwmIsCompositionEnabled");
    BOOL fEnabled;
    hr = (procDwmIsCompositionEnabled)(&fEnabled);
    if (SUCCEEDED(hr))
    {  
        if (!fEnabled)
        {
            // C4995: 'function': name was marked as #pragma deprecated
            #pragma warning(disable : 4995)
            PROCDwmEnableComposition procDwmEnableComposition = (PROCDwmEnableComposition)GetProcAddress(hinstLib, "DwmEnableComposition");
            hr = (*procDwmEnableComposition)(DWM_EC_ENABLECOMPOSITION);

            if (SUCCEEDED(hr))
            {
                hr = (procDwmIsCompositionEnabled)(&fEnabled);
                if (SUCCEEDED(hr) && fEnabled)
                {
                    // Enable Successfully
                    return S_OK;
                }
                else if(!fEnabled)
                {
                    const wchar_t szDWMMsg[] =
                        L"ERROR: 0x%X - The DWM is still disabled even after call DwmEnableComposition, but is required for running\r\n"
                        L"immersive applications. Please enable desktop composition and try again.\r\n";
                    wprintf_s(szDWMMsg, hr);
                }
                else
                {
                    const wchar_t szDWMMsg[] = L"ERROR: 0x%X - DwmIsCompositionEnabled return bad HResult.\r\n";
                    wprintf_s(szDWMMsg, hr);
                }
            }
            else
            {
                const wchar_t szDWMMsg[] =
                    L"ERROR: 0x%X - The DWM is currently disabled, but is required for running\r\n"
                    L"immersive applications. Please enable desktop composition and try again.\r\n";
                wprintf_s(szDWMMsg, hr);
            }
        }
    }
    else
    {
        const wchar_t szDWMMsg[] = L"ERROR: 0x%X - DwmIsCompositionEnabled return bad HResult.\r\n";
        wprintf_s(szDWMMsg, hr);
    }

    FreeLibrary(hinstLib);

    return hr;
}

void Run(__in const ArgInfo& info, __out DWORD& exitCode)
{
  Microsoft::WRL::Wrappers::RoInitializeWrapper roInitialize(RO_INIT_MULTITHREADED);

  if (FAILED(roInitialize))
  {
    wprintf_s(L"FAILED to initialize the Windows Runtime(0x%x)\n%s\n", static_cast<HRESULT>(roInitialize), MessageForHR(roInitialize).c_str());
    throw exception();
  }

  if (FAILED(_DoSystemStateChecks()))
  {
      throw exception();
  }

  setvbuf(stdout, NULL, _IONBF, 0);
  setvbuf(stderr, NULL, _IONBF, 0);

  //Lock the directory
  FileLock f(L"appxExecution.lock");
  //Copy disambigious files
  FileDisambiguator files(info.disambiguatePrefix, info.filesToDisambiguate, info.installOnly || !info.uninstallOnly, info.uninstallOnly || !info.installOnly);

  if (!FileExists(info.manifestPath))
    throw exception();

  auto app = CreateAppObject(info);
  if (info.uninstallOnly)
  {
    exitCode = 100;
    Uninstall(app);
  }
  else if (info.installOnly)
  {
    exitCode = 100;
    Install(app);
    wprintf_s(L"Package Full Name is %s\n", app->get_PackageFullName().c_str());
  }
  else
  {
    Install(app);
    Execute(app, info, exitCode);
    Uninstall(app);
  }
}
