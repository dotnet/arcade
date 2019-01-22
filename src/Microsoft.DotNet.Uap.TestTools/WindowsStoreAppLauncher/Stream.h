// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <wrl.h>
#include <memory>
#include <string>

using namespace Microsoft::WRL;
using namespace std;

class Stream : public RuntimeClass<RuntimeClassFlags<RuntimeClassType::ClassicCom>, IStream>
{
  shared_ptr<void> file;
  wstring name;
public:
  Stream(shared_ptr<void> f, const wstring& name);

  Stream(const wstring& name);

  virtual /* [local] */ HRESULT STDMETHODCALLTYPE Read(
    /* [annotation] */
    _Out_writes_bytes_to_(cb, *pcbRead)  void *pv,
    /* [annotation][in] */
    _In_  ULONG cb,
    /* [annotation] */
    _Out_opt_  ULONG *pcbRead) override;

  virtual /* [local] */ HRESULT STDMETHODCALLTYPE Write(
    /* [annotation] */
    _In_reads_bytes_(cb)  const void *pv,
    /* [annotation][in] */
    _In_  ULONG cb,
    /* [annotation] */
    _Out_opt_  ULONG *pcbWritten) override;
  virtual /* [local] */ HRESULT STDMETHODCALLTYPE Seek(
    /* [in] */ LARGE_INTEGER dlibMove,
    /* [in] */ DWORD dwOrigin,
    /* [annotation] */
    _Out_opt_  ULARGE_INTEGER *plibNewPosition) override;

  virtual HRESULT STDMETHODCALLTYPE SetSize(
    /* [in] */ ULARGE_INTEGER libNewSize) override;

  virtual /* [local] */ HRESULT STDMETHODCALLTYPE CopyTo(
    /* [annotation][unique][in] */
    _In_  IStream *pstm,
    /* [in] */ ULARGE_INTEGER cb,
    /* [annotation] */
    _Out_opt_  ULARGE_INTEGER *pcbRead,
    /* [annotation] */
    _Out_opt_  ULARGE_INTEGER *pcbWritten) override;

  virtual HRESULT STDMETHODCALLTYPE Commit(
    /* [in] */ DWORD grfCommitFlags) override;

  virtual HRESULT STDMETHODCALLTYPE Revert(void) override;

  virtual HRESULT STDMETHODCALLTYPE LockRegion(
    /* [in] */ ULARGE_INTEGER libOffset,
    /* [in] */ ULARGE_INTEGER cb,
    /* [in] */ DWORD dwLockType) override;

  virtual HRESULT STDMETHODCALLTYPE UnlockRegion(
    /* [in] */ ULARGE_INTEGER libOffset,
    /* [in] */ ULARGE_INTEGER cb,
    /* [in] */ DWORD dwLockType) override;

  virtual HRESULT STDMETHODCALLTYPE Stat(
    /* [out] */ __RPC__out STATSTG *pstatstg,
    /* [in] */ DWORD grfStatFlag) override;

  virtual HRESULT STDMETHODCALLTYPE Clone(
    /* [out] */ __RPC__deref_out_opt IStream **ppstm) override;
};