// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "App.h"
#include "Support.h"
#include <wrl.h>
#include <ShObjIdl.h>
#include <string>
#include <windows.foundation.h>
#include <Windows.Applicationmodel.Activation.h>

using namespace ABI::Windows::ApplicationModel::Activation;
using namespace ABI::Windows::Foundation::Internal;
using namespace ABI::Windows::Foundation;
using namespace Microsoft::WRL;
using namespace std;

class AppxApp : public App
{
private:
  ComPtr<IPackageDebugSettings> packageDebugSettings;
  wstring m_manifestPath, m_appId, m_packageId, m_packageFamilyName, stdoutFile, exitCodeFile;
  HANDLE m_hProcess;


  void InitStdoutFile(bool testApp);
  HRESULT GetAppContainerFolderPathInternal(wchar_t** appContainerFolderPath);

protected:
  template <class TResult, class TProgress>
  void Wait(
	  IAsyncOperationWithProgress<TResult, TProgress>* asyncOperation,
	  typename GetAbiType<typename IAsyncOperationWithProgress<TResult, TProgress>::TResult_complex>::type* result)
	{
	  // Create an event so we can wait for the async operation to complete
	  Event completeEvent(CreateEventEx(nullptr, nullptr, CREATE_EVENT_MANUAL_RESET, WRITE_OWNER | EVENT_ALL_ACCESS));
	  IFT(completeEvent.IsValid() ? S_OK : HRESULT_FROM_WIN32(GetLastError()));

	  auto completeHandler = Callback<IAsyncOperationWithProgressCompletedHandler<TResult, TProgress>>
	    ([&completeEvent](
	    IAsyncOperationWithProgress<TResult, TProgress>* asyncOperationWithProgress,
	    AsyncStatus asyncStatus)
	    -> HRESULT
	  {
	    UNREFERENCED_PARAMETER(asyncOperationWithProgress);
	    UNREFERENCED_PARAMETER(asyncStatus);
	    SetEvent(completeEvent.Get());
	    return S_OK;
	  });
	  IFT(asyncOperation->put_Completed(completeHandler.Get()));

	  // Wait for async operation to complete
	  IFT(WaitForSingleObjectEx(completeEvent.Get(), INFINITE, FALSE) != WAIT_FAILED ? S_OK : HRESULT_FROM_WIN32(GetLastError()));

	  // Check for errors
	  ComPtr<IAsyncInfo> asyncInfo;
	  HRESULT errorCode;

	  IFT(asyncOperation->QueryInterface(__uuidof(IAsyncInfo), &asyncInfo));
	  IFT(asyncInfo->get_ErrorCode(&errorCode));

	  IFT(errorCode);

	  // Get the result
	  if (result != nullptr)
	  {
	    IFT(asyncOperation->GetResults(result));
	  }
	}

  
  const wstring& get_ManifestPath() const throw();
  const wstring& get_PackageId() const throw();
  const wstring& get_PackageFamilyName() const throw();
  virtual ComPtr<IApplicationActivationManager> GetApplicationActivationManager() = 0;
  virtual wstring GetAppContainerFolderPath() const = 0;
public:
  AppxApp(const wstring& manifestPath, const wstring& appId);
  virtual ~AppxApp(void);

  virtual void Start(bool runInBackground, bool testApp, const wstring& executionArgs) override;
  virtual void Start(bool runInBackground, bool testApp) override;
  virtual void Stop() override;
  virtual DWORD WaitForExit() override;
  virtual DWORD WaitForExit(DWORD milliseconds, bool* result) override;

  virtual void EnableDebug(const wstring& debuggerCommandLine) override;
  virtual void DisableDebug() override;
  virtual wstring GetAppStdOutContent() override;  
  virtual wstring get_PackageFullName() const throw() override;
  virtual DWORD GetAppExitCode() override;
};
