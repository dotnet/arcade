// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <Windows.h>
#include <string>
#include <vector>
#include <memory>
using namespace std;

#define dprintf(...)
#define dwprintf(...)

typedef unique_ptr < void, add_pointer < decltype(CloseHandle)>::type> safe_handle;

#define MAX_WAIT_TIME (10 * 60 * 1000)
#define CHECK_INTERVAL (30 * 1000)

wstring MessageForHR(HRESULT hr);

#define IFR(function) if(FAILED(hr = function)) return hr;
#define IFER(function) if (function != 0) return E_FAIL;

#define IFT(function) com_error::ThrowIfFailed(function)

class FileLock
{
public:
    FileLock(const wchar_t* lockName);
private:
    // gets the number of seconds since the file has been created
    static unsigned int GetFileAccessDelta(const wchar_t* name);
    safe_handle handle;
};

class FileDisambiguator
{
public:
    FileDisambiguator(const wstring& prefix, const vector<wstring>& files, bool copy = true, bool del = true);
    ~FileDisambiguator();
private:
    wstring DisambiguateFile(const wstring& prefix, const wstring& name);
    vector<wstring> disambiguatedFiles;
    bool copy;
    bool del;
};

string make_string(wstring w);

class com_error : public exception
{
  string message;
  HRESULT hr;
public:
  com_error(HRESULT hr)
    :hr(hr), message(make_string(MessageForHR(hr)))
  {
  }

  com_error(HRESULT hr, string function)
    :hr(hr), message(function + ", " + make_string(MessageForHR(hr)))
  {

  }
  const char* what() const throw()
  {
    return message.c_str();
  }

  const HRESULT HR() const throw()
  {
    return hr;
  }
  static void ThrowIfFailed(HRESULT hr)
  {
    if (hr < 0)
      throw com_error(hr);
  }
  static void ThrowIfFailed(HRESULT hr, string function)
  {
    if (hr < 0)
      throw com_error(hr, function);
  }
};

#undef GetEnvironmentVariable

wstring GetEnvironmentVariable(wstring name);


namespace Reg
{
  typedef unique_ptr<remove_pointer<HKEY>::type, decltype(&RegCloseKey)> RegistryKey;
  enum struct Key
  {
    HKCR,
    HKCC,
    HKCU,
    HKLM,
    HKU
  };
  enum struct Access
  {
    Read,
    Write
  };
  RegistryKey CreateKey(Key key, const wstring& subkey, Access access);
  DWORD GetValueOrDefault(const RegistryKey& key, const wstring& valueName, DWORD fallbackValue);
  void SetValue(const RegistryKey& key, const wstring& valueName, DWORD value);
}


