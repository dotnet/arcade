// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "stdafx.h"
#include "Stream.h"
#include "Support.h"

#pragma warning(disable:4100) // unreferenced formal parameter

Stream::Stream(const wstring& name) : name(name)
{
  file = shared_ptr<void>(CreateFile(name.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL), CloseHandle);
}

Stream::Stream(shared_ptr<void> f, const wstring& name) : name(name), file(f)
{
}

HRESULT STDMETHODCALLTYPE Stream::Read(
  /* [annotation] */
  _Out_writes_bytes_to_(cb, *pcbRead)  void *pv,
  /* [annotation][in] */
  _In_  ULONG cb,
  /* [annotation] */
  _Out_opt_  ULONG *pcbRead)
{
  dprintf("Read called...\n");
  ReadFile(file.get(), pv, cb, pcbRead, NULL);
  return HRESULT_FROM_WIN32(GetLastError());
}

HRESULT STDMETHODCALLTYPE Stream::Write(
  /* [annotation] */
  _In_reads_bytes_(cb)  const void *pv,
  /* [annotation][in] */
  _In_  ULONG cb,
  /* [annotation] */
  _Out_opt_  ULONG *pcbWritten)
{
  dprintf("Write called...\n");
  return NTE_NOT_SUPPORTED;
}
HRESULT STDMETHODCALLTYPE Stream::Seek(
  /* [in] */ LARGE_INTEGER dlibMove,
  /* [in] */ DWORD dwOrigin,
  /* [annotation] */
  _Out_opt_  ULARGE_INTEGER *plibNewPosition)
{
  dprintf("Seek called... dlibMove: %lli dwOrigin: %u\n", dlibMove.QuadPart, dwOrigin);
  LONG high = dlibMove.HighPart;
  dprintf("Low : %u\n", dlibMove.LowPart);
  dprintf("High : %i\n", high);
  dprintf("Move method : %u\n", dwOrigin);
  DWORD low = SetFilePointer(file.get(), dlibMove.LowPart, &high, dwOrigin);
  HRESULT hr = HRESULT_FROM_WIN32(GetLastError());
  if (SUCCEEDED(hr) && plibNewPosition)
  {
    plibNewPosition->HighPart = high;
    plibNewPosition->LowPart = low;
  }
  if (FAILED(hr))
  {
    dprintf("%S FAILED(0x%x) %S: %i\n%s\n", "SetFilePointer", hr, __FILE__, __LINE__, MessageForHR(hr).c_str()); \
  }
  return hr;
}
HRESULT STDMETHODCALLTYPE Stream::SetSize(
  /* [in] */ ULARGE_INTEGER libNewSize)
{
  dprintf("SetSize called...\n");
  return NTE_NOT_SUPPORTED;
}

HRESULT STDMETHODCALLTYPE Stream::CopyTo(
  /* [annotation][unique][in] */
  _In_  IStream *pstm,
  /* [in] */ ULARGE_INTEGER cb,
  /* [annotation] */
  _Out_opt_  ULARGE_INTEGER *pcbRead,
  /* [annotation] */
  _Out_opt_  ULARGE_INTEGER *pcbWritten)
{
  dprintf("CopyTo called...\n");
  return NTE_NOT_SUPPORTED;
}

HRESULT STDMETHODCALLTYPE Stream::Commit(
  /* [in] */ DWORD grfCommitFlags)
{
  dprintf("Commit called...\n");
  return S_OK;
}

HRESULT STDMETHODCALLTYPE Stream::Revert(void)
{
  dprintf("Revert called...\n");
  return S_OK;
}

HRESULT STDMETHODCALLTYPE Stream::LockRegion(
  /* [in] */ ULARGE_INTEGER libOffset,
  /* [in] */ ULARGE_INTEGER cb,
  /* [in] */ DWORD dwLockType)
{
  dprintf("LockRegion called...\n");
  return NTE_NOT_SUPPORTED;
}

HRESULT STDMETHODCALLTYPE Stream::UnlockRegion(
  /* [in] */ ULARGE_INTEGER libOffset,
  /* [in] */ ULARGE_INTEGER cb,
  /* [in] */ DWORD dwLockType)
{
  dprintf("UnlockRegion called...\n");
  return NTE_NOT_SUPPORTED;
}

HRESULT STDMETHODCALLTYPE Stream::Stat(
  /* [out] */ __RPC__out STATSTG *pstatstg,
  /* [in] */ DWORD grfStatFlag)
{
  dprintf("Stat called... grdStatFlag : %u\n", grfStatFlag);
  if (!pstatstg)
    return E_POINTER;
  if (grfStatFlag != STATFLAG_NONAME)
  {
    return NTE_NOT_SUPPORTED;
  }
  pstatstg->type = STGTY_STORAGE;
  DWORD high;
  DWORD low = GetFileSize(file.get(), &high);
  if (low == INVALID_FILE_SIZE)
    return HRESULT_FROM_WIN32(GetLastError());
  pstatstg->cbSize.LowPart = low;
  pstatstg->cbSize.HighPart = high;
  DWORD err = GetFileTime(file.get(), &pstatstg->ctime, &pstatstg->atime, &pstatstg->mtime);
  if (err == 0)
    return HRESULT_FROM_WIN32(GetLastError());
  pstatstg->grfMode = STGM_READ;
  pstatstg->grfLocksSupported = 0;
  pstatstg->clsid = CLSID_NULL;
  pstatstg->grfStateBits = 0;
  pstatstg->reserved = 0;
  return S_OK;
}

HRESULT STDMETHODCALLTYPE Stream::Clone(
  /* [out] */ __RPC__deref_out_opt IStream **ppstm)
{
  dprintf("Clone called...\n");
  if (ppstm)
  {
    *ppstm = Make<Stream>(file, name).Detach();
  }
  return S_OK;
}