// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <Windows.h>
#include <string>
using namespace std;

class App
{
public:
  virtual ~App()
  {}
  virtual void Add() = 0;
  virtual void Remove() = 0;
    
  virtual void Start(bool runInBackground, bool testApp, const wstring& executionArgs) = 0;
  virtual void Start(bool runInBackground, bool testApp) = 0;
  virtual void Stop() = 0;
  virtual DWORD WaitForExit() = 0;
  virtual DWORD WaitForExit(DWORD milliseconds, bool* result) = 0;

  virtual void EnableDebug(const wstring& debuggerCommandLine) = 0;
  virtual void DisableDebug() = 0;

  virtual wstring GetAppStdOutContent() = 0;
  virtual DWORD GetAppExitCode() = 0;
  virtual wstring get_PackageFullName() const throw() = 0;
};