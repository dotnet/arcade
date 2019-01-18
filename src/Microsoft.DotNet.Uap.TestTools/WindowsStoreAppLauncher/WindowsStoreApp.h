// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "AppxApp.h"
#include <memory>
#include <string>

#include <wrl.h>
#include <Shobjidl.h>
#include <windows.foundation.h>

using namespace ABI::Windows::Foundation;
using namespace Microsoft::WRL;
using namespace std;

class WindowsStoreApp : public AppxApp
{
protected:
  virtual ComPtr<IApplicationActivationManager> GetApplicationActivationManager() override;
  virtual wstring GetAppContainerFolderPath() const override;
public:
  WindowsStoreApp(const wstring& manifestPath, const wstring& appId);
  virtual ~WindowsStoreApp();

  virtual void Add() override;
  virtual void Remove() override;
};

