// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <string.h>
#include <wchar.h>
#include "stdafx.h"
#include "AppxApp.h"
#include "Support.h"
#include "Stream.h"


using namespace std;
using namespace ABI::Windows::ApplicationModel::Activation;
using namespace ABI::Windows::Management::Deployment;
using namespace ABI::Windows::Foundation;
using namespace Microsoft::WRL;

typedef
unique_ptr<wchar_t, void(*)(wchar_t*)>
unique_char_array;

void unique_char_deleter(wchar_t* arr)
{
  delete [] arr;
}

AppxApp::AppxApp(const wstring& mainfestPath, const wstring& appId)
:m_manifestPath(mainfestPath), m_appId(appId), m_hProcess(nullptr)
{
  // Get the full path to the appx manifest file
  unique_ptr<wchar_t, void(__cdecl *)(LPVOID)> manifestFullPath(reinterpret_cast<wchar_t*>(malloc(MAX_PATH * sizeof(wchar_t))), free);
  IFT(GetFullPathName(m_manifestPath.c_str(), MAX_PATH, manifestFullPath.get(), nullptr) != 0 ? S_OK : HRESULT_FROM_WIN32(GetLastError()));
  m_manifestPath = manifestFullPath.get();

  // Create AppxFactory
  dprintf("Creating AppxFactory...\n");
  ComPtr<IAppxFactory> appxFactory;
  IFT(CoCreateInstance(__uuidof(AppxFactory), nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&appxFactory)));

  // Create IStream for Appx manifest file
  dprintf("Creating Stream...\n");
  ComPtr<IStream> appxManifestPathStream = Make<Stream>(m_manifestPath);

  // Read the AppXManifest.xml file
  dprintf("Creating Manifest Reader...\n");
  ComPtr<IAppxManifestReader> appxManifestReader;
  IFT(appxFactory->CreateManifestReader(appxManifestPathStream.Get(), &appxManifestReader));

  dprintf("Creating PackageID...\n");
  // Get the AppxManifestPackageId
  ComPtr<IAppxManifestPackageId> appxManifestPackageId;
  IFT(appxManifestReader->GetPackageId(&appxManifestPackageId));

  dprintf("Getting Package Full Name...\n");
  // Get the package full name
  LPWSTR packageId;
  IFT(appxManifestPackageId->GetPackageFullName(&packageId));
  m_packageId = packageId;
  dwprintf(L"%s\n", m_packageId.c_str());

  dprintf("Getting Package Family Name...\n");
  // Get the package family name
  LPWSTR packageFamilyName;
  IFT(appxManifestPackageId->GetPackageFamilyName(&packageFamilyName));
  m_packageFamilyName = packageFamilyName;
  dwprintf(L"%s\n", m_packageFamilyName.c_str());

  if (m_appId.empty()) // If App Id is empty get it from the manifest file
  {
    ComPtr<IAppxManifestApplicationsEnumerator> appxManifestApplicationsEnumerator;
    ComPtr<IAppxManifestApplication> appxManifestApplication;
    BOOL hasCurrent;
    LPWSTR appUserModelId;

    dprintf("Getting Applications...\n");
    IFT(appxManifestReader->GetApplications(&appxManifestApplicationsEnumerator));
    IFT(appxManifestApplicationsEnumerator->GetHasCurrent(&hasCurrent));

    // If the enumerator is empty can't choose a default app
    if (!hasCurrent)
    {
      throw com_error(E_INVALIDARG);
    }

    IFT(appxManifestApplicationsEnumerator->GetCurrent(&appxManifestApplication));
    IFT(appxManifestApplicationsEnumerator->MoveNext(&hasCurrent));

    // If the enumerator has more than one item it is ambiguous what app to choose
    if (hasCurrent)
    {
      throw com_error(E_INVALIDARG);
    }

    dprintf("Getting UserModelId...\n");
    IFT(appxManifestApplication->GetAppUserModelId(&appUserModelId));
    m_appId = appUserModelId;
  }
  else // Else prepend the package family name to the supplied App Id
  {
    dprintf("Getting UserModelId...\n");
    m_appId = m_packageFamilyName + L"!" + m_appId;
  }
  dwprintf(L"%s\n", m_appId.c_str());
}

AppxApp::~AppxApp()
{
  if (m_hProcess != nullptr)
  {
    CloseHandle(m_hProcess);
  }
}

const wstring& AppxApp::get_ManifestPath() const throw()
{
  return m_manifestPath;
}

const wstring& AppxApp::get_PackageId() const throw()
{
  return m_packageId;
}

const wstring& AppxApp::get_PackageFamilyName() const throw()
{
  return m_packageFamilyName;
}

wstring AppxApp::get_PackageFullName() const throw()
{
  return m_packageId;
}

typedef BOOL(WINAPI *fn_AssignProcessToJobObject)(_In_ HANDLE hJob, _In_ HANDLE hProcess);
typedef HANDLE(WINAPI *fn_OpenJobObject)(_In_ DWORD dwDesiredAccess, _In_ BOOL bInheritHandles, _In_ LPCWSTR lpName);
typedef DWORD(WINAPI *fn_GetProcessImageFileName)(_In_ HANDLE hProcess, _Out_ LPWSTR lpImageFileName, _In_ DWORD nSize);
typedef DWORD(WINAPI *fn_GetEnvironmentVariable)(_In_opt_ LPCWSTR lpName, _Out_opt_ LPWSTR lpBuffer, _In_ DWORD nSize);

fn_AssignProcessToJobObject p_AssignProcessToJobObject;
fn_OpenJobObject p_OpenJobObject;
fn_GetProcessImageFileName p_GetProcessImageFileName;
fn_GetEnvironmentVariable p_GetEnvironmentVariable;


void ProcessSmartyJob(HANDLE hProcess, const wchar_t* manifestPath)
{
  unique_ptr<remove_pointer<HMODULE>::type, decltype(&FreeLibrary)> kernel32(LoadLibraryEx(L"kernel32.dll", NULL, NULL), &FreeLibrary);
  if (kernel32.get() == NULL)
    return;
  unique_ptr<remove_pointer<HMODULE>::type, decltype(&FreeLibrary)> psapi(LoadLibraryEx(L"api-ms-win-core-psapi-l1-1-0.dll", NULL, NULL), &FreeLibrary);
  if (psapi.get() == NULL)
    return;
  unique_ptr<remove_pointer<HMODULE>::type, decltype(&FreeLibrary)> procEnv(LoadLibraryEx(L"api-ms-win-core-processenvironment-l1-2-0.dll", NULL, NULL), &FreeLibrary);
  if (procEnv.get() == NULL)
    return;
  p_OpenJobObject = (fn_OpenJobObject) GetProcAddress(kernel32.get(), "OpenJobObject");
  p_AssignProcessToJobObject = (fn_AssignProcessToJobObject) GetProcAddress(kernel32.get(), "AssignProcessToJobObjectW");
  p_GetProcessImageFileName = (fn_GetProcessImageFileName) GetProcAddress(psapi.get(), "K32GetProcessImageFileNameW");
  p_GetEnvironmentVariable = (fn_GetEnvironmentVariable) GetProcAddress(procEnv.get(), "GetEnvironmentVariableW");
  auto varName = L"SMARTY_JOB_ID";
  DWORD sizeNeeded = (*p_GetEnvironmentVariable)(varName, nullptr, 0);
  if (sizeNeeded == 0)
  {
    DWORD err = GetLastError();
    if (err == ERROR_ENVVAR_NOT_FOUND)
    {
      return;
    }
    else
    {
      auto msg = MessageForHR(HRESULT_FROM_WIN32(err));
      fwprintf_s(stderr, L"Error reading environment:\n%s\n", msg.c_str());
      throw runtime_error(string(msg.begin(), msg.end()));
    }
  }
  unique_char_array smartyJobId(new wchar_t[sizeNeeded], unique_char_deleter);
  DWORD size = (*p_GetEnvironmentVariable)(L"SMARTY_JOB_ID", smartyJobId.get(), sizeNeeded);
  if (size == 0)
  {
    DWORD err = GetLastError();
    if (err == ERROR_ENVVAR_NOT_FOUND)
      return;
    else
    {
      fwprintf_s(stderr, L"Error reading environment:\n%s\n", MessageForHR(HRESULT_FROM_WIN32(err)).c_str());
      return;
    }
  }
  unique_char_array appxProcessFileName(new wchar_t[MAX_PATH], unique_char_deleter);
  if ((*p_GetProcessImageFileName)(hProcess, appxProcessFileName.get(), MAX_PATH) == 0)
  {
    fwprintf_s(stderr, L"Error getting process file name:\n%s\n", MessageForHR(HRESULT_FROM_WIN32(GetLastError())).c_str());
    return;
  }
  HRESULT hr = PathCchRemoveFileSpec(appxProcessFileName.get(), MAX_PATH);
  if (FAILED(hr))
  {
    fwprintf_s(stderr, L"Error processing path:\n%s\n", MessageForHR(hr).c_str());
    return;
  }
  unique_char_array manifestDirectory(new wchar_t[MAX_PATH], unique_char_deleter);
  wcscpy_s(manifestDirectory.get(), MAX_PATH, manifestPath);
  hr = PathCchRemoveFileSpec(manifestDirectory.get(), MAX_PATH);
  if (FAILED(hr))
  {
    fwprintf_s(stderr, L"Error processing path:\n%s\n", MessageForHR(hr).c_str());
    return;
  }
  if (wcscmp(appxProcessFileName.get(), manifestDirectory.get()) == 0)
  {
    // handle is the appx process handle
    // and SMARTY_JOB_ID is set
    HANDLE tmp = (*p_OpenJobObject)(JOB_OBJECT_ASSIGN_PROCESS, FALSE, smartyJobId.get());
    if (tmp == NULL)
    {
      fwprintf_s(stderr, L"Error opening job object %s:\n%s\n", smartyJobId.get(), MessageForHR(HRESULT_FROM_WIN32(GetLastError())).c_str());
      return;
    }
    unique_ptr < void, add_pointer<decltype(CloseHandle)>::type> jobHandle(tmp, &CloseHandle);
    BOOL ret = (*p_AssignProcessToJobObject)(jobHandle.get(), hProcess);
    if (ret == 0)
    {
      fwprintf_s(stderr, L"Error assigning appx process to job:\n%s\n%s\n", smartyJobId.get(), MessageForHR(HRESULT_FROM_WIN32(GetLastError())).c_str());
      return;
    }
  }
}

void AppxApp::InitStdoutFile(bool testApp)
{
  if (testApp)
  {
    wstring appContainerPath = GetAppContainerFolderPath();

    WIN32_FILE_ATTRIBUTE_DATA stdoutFileAttributeData;
    stdoutFile = appContainerPath +  L"\\LocalState\\AC\\stdout.txt";

    // Delete the stdout file if it exists
    if (GetFileAttributesEx(stdoutFile.c_str(), ::GET_FILEEX_INFO_LEVELS::GetFileExInfoStandard, &stdoutFileAttributeData))
    {
      if (!DeleteFile(stdoutFile.c_str()))
      {
        throw com_error(HRESULT_FROM_WIN32(GetLastError()));
      }
    }

    // ExitCode.txt stores process Exit Code
    WIN32_FILE_ATTRIBUTE_DATA exitCodeFileAttributeData;
    exitCodeFile = appContainerPath + L"\\AC\\Temp\\exitcode.txt";
    if (GetFileAttributesEx(exitCodeFile.c_str(), ::GET_FILEEX_INFO_LEVELS::GetFileExInfoStandard, &exitCodeFileAttributeData))
    {
      if (!DeleteFile(exitCodeFile.c_str()))
      {
        throw com_error(HRESULT_FROM_WIN32(GetLastError()));
      }
    }
  }
}

void AppxApp::Start(bool runInBackground, bool testApp)
{
  Start(runInBackground, testApp, L"");
}

void AppxApp::Start(bool runInBackground, bool testApp, const wstring& executionArgs)
{
  InitStdoutFile(testApp);

  ComPtr<IApplicationActivationManager> applicationActivationManager = GetApplicationActivationManager();
  DWORD dwProcessId;

  ACTIVATEOPTIONS options = ACTIVATEOPTIONS::AO_NOSPLASHSCREEN;
  if (!runInBackground)
    options = ACTIVATEOPTIONS::AO_NONE;

  IFT(applicationActivationManager->ActivateApplication(m_appId.c_str(), executionArgs.c_str(), options, &dwProcessId));
  dwprintf(L"Process started, pid: %d\n", dwProcessId);
  // Get a process handle to the app
  // PROCESS_SET_QUOTA is required for using AssignProcessToJobObject
  m_hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | SYNCHRONIZE | PROCESS_TERMINATE | PROCESS_SET_QUOTA, FALSE, dwProcessId);

  if (m_hProcess == nullptr)
  {
    DWORD openProcessError = GetLastError();

    // If the error is ERROR_INVALID_PARAMETER then the process exited before we could open a handle to it
    if (openProcessError != ERROR_INVALID_PARAMETER)
    {
      throw com_error(HRESULT_FROM_WIN32(openProcessError));
    }
  }
  ProcessSmartyJob(m_hProcess, m_manifestPath.c_str());
}

void AppxApp::Stop()
{
  if (m_hProcess != nullptr)
  {
    if (!TerminateProcess(m_hProcess, (UINT) -1))
    {
      throw com_error(HRESULT_FROM_WIN32(GetLastError()));
    }
  }
}

DWORD AppxApp::WaitForExit()
{
  bool result;
  return WaitForExit(INFINITE, &result);
}

DWORD AppxApp::WaitForExit(DWORD milliseconds, bool* result)
{
  if (m_hProcess == nullptr)
  {
    throw com_error(E_POINTER);
  }

  DWORD waitResult = WaitForSingleObjectEx(m_hProcess, milliseconds, FALSE);

  if (waitResult == WAIT_TIMEOUT)
  {
    *result = false;
    return (DWORD) -1;
  }
  else if (waitResult != WAIT_FAILED)
  {
    *result = true;
    DWORD exitCode = GetAppExitCode();
    return exitCode;
  }
  else
  {
    throw com_error(HRESULT_FROM_WIN32(GetLastError()));
  }
}

void AppxApp::EnableDebug(const wstring& debuggerCommandLine)
{
  if (packageDebugSettings == nullptr)
    IFT(CoCreateInstance(CLSID_PackageDebugSettings, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&packageDebugSettings)));
  wprintf_s(L"Package ID is %s\n", m_packageId.c_str());
  IFT(packageDebugSettings->EnableDebugging(m_packageId.c_str(), debuggerCommandLine.empty() ? nullptr : debuggerCommandLine.c_str(), nullptr));
}

void AppxApp::DisableDebug()
{
  if (packageDebugSettings == nullptr)
    IFT(CoCreateInstance(CLSID_PackageDebugSettings, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&packageDebugSettings)));
  IFT(packageDebugSettings->DisableDebugging(m_packageId.c_str()));
}

// The package must be installed for this to succeed
HRESULT AppxApp::GetAppContainerFolderPathInternal(wchar_t** appContainerFolderPath)
{
  HRESULT hr;

  // Get the app container folder path
  PSID tempsid;
  IFR(DeriveAppContainerSidFromAppContainerName(m_packageFamilyName.c_str(), &tempsid));
  std::unique_ptr<void, PVOID(__stdcall *)(PSID)> sid(tempsid, &FreeSid);

  wchar_t* tempSidString;
  IFR(ConvertSidToStringSid(sid.get(), &tempSidString) ? S_OK : HRESULT_FROM_WIN32(GetLastError()));
  std::unique_ptr<wchar_t, HLOCAL(__stdcall *)(HLOCAL)> sidString(tempSidString, LocalFree);

  wchar_t* tempfolderPath;
  IFR(::GetAppContainerFolderPath(sidString.get(), &tempfolderPath));
  std::unique_ptr<wchar_t, void(__stdcall *)(LPVOID)> folderPath(tempfolderPath, CoTaskMemFree);

  // Remove the trailing \AC from the path
  size_t pathLength = wcsnlen_s(folderPath.get(), SHORT_MAX);

  if (pathLength < 3)
  {
    return E_FAIL;
  }

  if (wcscmp(L"\\AC", folderPath.get() + (pathLength - 3)) != 0)
  {
    return E_FAIL;
  }

  folderPath.get()[pathLength - 3] = '\0';

  *appContainerFolderPath = folderPath.release();

  return hr;
}

wstring AppxApp::GetAppStdOutContent()
{
  try
  {
    wifstream file(stdoutFile);
    return wstring((istreambuf_iterator<wchar_t>(file)), istreambuf_iterator<wchar_t>());
  }
  catch (...)
  {
    return L"(null)";
  }
}

// Some UI (or Jscript) app couldn't  get its exit code directly
// So App itself are supposed to write a file("exitcode.txt") into %temp% folder
// then AppLauncher will fetch its vlaue if this file exists
DWORD AppxApp::GetAppExitCode()
{
  DWORD exitCode;

  // Check exitCode.txt file first
  ifstream file(exitCodeFile);
  if (file) 
  {
    char content[256];
    memset(content, '\0', 256 * sizeof(char));
    file.getline(content, 256);
    exitCode = strtol(content, NULL, 0);
    wprintf_s(L"Process exited with return code(from exitcode.txt) %d.\n", exitCode);
  }
  else
  {
    if (GetExitCodeProcess(m_hProcess, &exitCode) != 0)
      wprintf_s(L"Process has just exited with return code %d.\n", exitCode);
  }

  return exitCode;
}
