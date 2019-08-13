// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "stdafx.h"
#include "Support.h"

string make_string(wstring w)
{
  return string(w.begin(), w.end());
}

FileLock::FileLock(const wchar_t* lockName) : handle(nullptr, CloseHandle)
{
  handle = safe_handle(CreateFile(lockName, GENERIC_READ | GENERIC_WRITE, 0, NULL, CREATE_NEW, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_DELETE_ON_CLOSE, NULL), CloseHandle);
  while (handle.get() == INVALID_HANDLE_VALUE)
  {
    DWORD err = GetLastError();
    if (err == ERROR_FILE_EXISTS)
    {
      if (GetFileAccessDelta(lockName) > MAX_WAIT_TIME)
      {
        handle = safe_handle(CreateFile(lockName, GENERIC_READ | GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_DELETE_ON_CLOSE, NULL), CloseHandle);
      }
      else
      {
        Sleep(CHECK_INTERVAL);
        handle = safe_handle(CreateFile(lockName, GENERIC_READ | GENERIC_WRITE, 0, NULL, CREATE_NEW, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_DELETE_ON_CLOSE, NULL), CloseHandle);
      }
    }
    else
    {
      throw com_error(HRESULT_FROM_WIN32(err));
    }
  }
}

/*static*/ unsigned int FileLock::GetFileAccessDelta(const wchar_t* name)
{
  WIN32_FILE_ATTRIBUTE_DATA data;
  if (!GetFileAttributesEx(name, GetFileExInfoStandard, &data))
  {
    return (unsigned int) 0x7FFFFFFF;
  }
  FILETIME systemT;
  GetSystemTimeAsFileTime(&systemT);
  ULARGE_INTEGER creationTime = *(ULARGE_INTEGER*) &data.ftCreationTime;
  ULARGE_INTEGER systemTime = *(ULARGE_INTEGER*) &systemT;
  return (unsigned int) ((systemTime.QuadPart - creationTime.QuadPart) / 10000u);
}

FileDisambiguator::FileDisambiguator(const wstring& prefix, const vector<wstring>& files, bool copy, bool del)
:copy(copy), del(del)
{
  for (auto f : files)
  {
    this->disambiguatedFiles.push_back(DisambiguateFile(prefix, f));
  }
}

wstring FileDisambiguator::DisambiguateFile(const wstring& prefix, const wstring& name)
{
  // calculate the source file name
  auto seperatorIndex = name.rfind('\\');
  wstring src;
  dwprintf(L"Seperator index: %lld\n", seperatorIndex);
  if (seperatorIndex != wstring::npos)
  {
    wstring dir = name.substr(0, seperatorIndex);
    wstring fname = name.substr(seperatorIndex + 1);
    src = dir + L"\\" + prefix + L"_" + fname;
  }
  else
  {
    src = prefix + L"_" + name;
  }


  wprintf_s(L"copying %s -> %s\n", src.c_str(), name.c_str());
  BOOL cancel = false;
  if (copy && !CopyFileEx(src.c_str(), name.c_str(), NULL, NULL, &cancel, COPY_FILE_FAIL_IF_EXISTS))
  {
    DWORD err = GetLastError();
    wprintf_s(L"Error copying file:\n%s\n", MessageForHR(HRESULT_FROM_WIN32(err)).c_str());
    if (err == ERROR_FILE_EXISTS)
      return name.c_str();
    return L"";
  }
  else
  {
    return name.c_str();
  }
}

FileDisambiguator::~FileDisambiguator()
{
  for (auto f : this->disambiguatedFiles)
  {
    if (del && !f.empty())
    {
      wprintf_s(L"deleting %s...\n", f.c_str());
      DeleteFile(f.c_str());
    }
  }
}

typedef DWORD(WINAPI *fn_FormatMessage)(_In_ DWORD dwFlags, _In_opt_ LPCVOID lpSource, _In_ DWORD dwMessageId, _In_ DWORD dwLanguageId, _Out_ LPWSTR lpBuffer, _In_ DWORD nSize, _In_opt_ va_list * Arguments);

bool haveFormatMessage = true;
fn_FormatMessage p_FormatMessage = NULL;

DWORD WINAPI _formatMessage(_In_ DWORD dwFlags, _In_opt_ LPCVOID lpSource, _In_ DWORD dwMessageId, _In_ DWORD dwLanguageId, _Out_ LPWSTR lpBuffer, _In_ DWORD nSize, _In_opt_ va_list * Arguments)
{
  if (p_FormatMessage == NULL && haveFormatMessage)
  {
    //this will be leaked, but we don't need to free this until process shutdown anyways
    HMODULE locModule = LoadLibraryEx(L"api-ms-win-core-localization-l1-2-1.dll", NULL, NULL);
    if (locModule == NULL)
    {
      haveFormatMessage = false;
      return (DWORD) -1;
    }
    p_FormatMessage = (fn_FormatMessage) GetProcAddress(locModule, "FormatMessageW");
    return (*p_FormatMessage)(dwFlags, lpSource, dwMessageId, dwLanguageId, lpBuffer, nSize, Arguments);
  }
  else if (haveFormatMessage)
  {
    return (*p_FormatMessage)(dwFlags, lpSource, dwMessageId, dwLanguageId, lpBuffer, nSize, Arguments);
  }
  else
  {
    return (DWORD) -1;
  }
}

wstring MessageForHR(HRESULT hr)
{
  LPWSTR msg;
  DWORD retval = _formatMessage(
    FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM,
    NULL,
    hr,
    MAKELANGID(LANG_ENGLISH, SUBLANG_ENGLISH_US),
    (LPWSTR) &msg,
    1,
    NULL);
  wstring ret;
  if (retval != 0)
    ret = wstring(msg);
  else
  {
    wstringstream ss;
    ss << L"0x" << hex << hr;
    ret = ss.str();
  }
  LocalFree((HLOCAL) msg);
  return ret;
}

wstring GetEnvironmentVariable(wstring name)
{
  LPWSTR buf = new wchar_t[_MAX_ENV];
  if (!GetEnvironmentVariableW(name.c_str(), buf, _MAX_ENV))
  {
    wstring ret = L"";
    delete [] buf;
    return ret;
  }
  else
  {
    wstring ret = buf;
    delete [] buf;
    return ret;
  }
}

using namespace Reg;

HKEY GetHKey(Key key)
{

  switch (key)
  {
    case Key::HKCR:
      return HKEY_CLASSES_ROOT;
    case Key::HKCC:
      return HKEY_CURRENT_CONFIG;
    case Key::HKCU:
      return HKEY_CURRENT_USER;
    case Key::HKLM:
      return HKEY_LOCAL_MACHINE;
    case Key::HKU:
      return HKEY_USERS;
    default:
      throw com_error(E_FAIL);
  }
}

REGSAM GetAccess(Access access)
{
  switch (access)
  {
    case Access::Read:
      return KEY_READ;
    case Access::Write:
      return KEY_WRITE;
    default:
      throw com_error(E_FAIL);
  }
}

RegistryKey Reg::CreateKey(Key key, const wstring& subkey, Access access)
{
  HKEY tmpKey;
  DWORD err = RegCreateKeyEx(
      GetHKey(key),
      subkey.c_str(),
      0,
      NULL,
      REG_OPTION_NON_VOLATILE,
      GetAccess(access),
      NULL,
      &tmpKey,
      NULL
      );
  if (err != ERROR_SUCCESS)
  {
    throw com_error(HRESULT_FROM_WIN32(err));
  }
  return RegistryKey(tmpKey, &RegCloseKey);
}

DWORD Reg::GetValueOrDefault(const RegistryKey& key, const wstring& valueName, DWORD defaultValue)
{
  DWORD data;
  DWORD cbData = sizeof(data);
  DWORD err = RegGetValue(key.get(), NULL, valueName.c_str(),
      RRF_RT_DWORD,
      NULL,
      &data,
      &cbData);
  if (err == ERROR_NOT_FOUND || err == ERROR_FILE_NOT_FOUND)
  {
    data = defaultValue;
  }
  else if (err != ERROR_SUCCESS)
  {
    throw com_error(HRESULT_FROM_WIN32(err));
  }
  return data;
}

void Reg::SetValue(const RegistryKey& key, const wstring& valueName, DWORD value)
{
  DWORD err = RegSetValueEx(
    key.get(),
    valueName.c_str(),
    0,
    REG_DWORD,
    reinterpret_cast<LPBYTE>(&value),
    sizeof(DWORD));
  if (err != ERROR_SUCCESS)
  {
    throw com_error(HRESULT_FROM_WIN32(err));
  }
}

