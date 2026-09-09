// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Microsoft.DotNet.CMake.Sdk;

/// <summary>
/// Executes a command while holding a named, cross-process mutex.
/// </summary>
public sealed class ExecWithMutex : Exec
{
    [Required]
    public string MutexName { get; set; }

    public override bool Execute()
    {
        using var mutex = CreateMutex(MutexName);
        bool ownsMutex = false;

        try
        {
            try
            {
                mutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
            }

            ownsMutex = true;
            return base.Execute();
        }
        finally
        {
            if (ownsMutex)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private Mutex CreateMutex(string path)
    {
        string key = TaskEnvironment.GetAbsolutePath(path).GetCanonicalForm().Value;
        using var hasher = SHA256.Create();
        string hash = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(key)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

#if NETFRAMEWORK
        return new Mutex(false, $"Local\\Microsoft.DotNet.CMake.Sdk.{hash}");
#else
        return new Mutex(
            false,
            $"Microsoft.DotNet.CMake.Sdk.{hash}",
            new NamedWaitHandleOptions { CurrentUserOnly = true },
            out _);
#endif
    }
}
