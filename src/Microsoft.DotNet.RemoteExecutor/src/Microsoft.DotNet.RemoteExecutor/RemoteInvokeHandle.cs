// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.DotNet.RemoteExecutor
{
    /// <summary>
    /// A cleanup handle to the Process created for the remote invocation.
    /// </summary>
    public sealed class RemoteInvokeHandle : IDisposable
    {
        public RemoteInvokeHandle(Process process, RemoteInvokeOptions options, string assemblyName = null, string className = null, string methodName = null)
        {
            Process = process;
            Options = options;
            AssemblyName = assemblyName;
            ClassName = className;
            MethodName = methodName;
        }

        public int ExitCode
        {
            get
            {
                Process.WaitForExit();
                return Process.ExitCode;
            }
        }

        public Process Process { get; set; }

        public RemoteInvokeOptions Options { get; private set; }

        public string AssemblyName { get; private set; }

        public string ClassName { get; private set; }

        public string MethodName { get; private set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this); // before Dispose(true) in case the Dispose call throws
            Dispose(disposing: true);
        }

        private void Dispose(bool disposing)
        {
            Assert.True(disposing, $"A test {AssemblyName}!{ClassName}.{MethodName} forgot to Dispose() the result of RemoteInvoke()");

            if (Process != null)
            {
                // A bit unorthodox to do throwing operations in a Dispose, but by doing it here we avoid
                // needing to do this in every derived test and keep each test much simpler.
                try
                {
                    Assert.True(Process.WaitForExit(Options.TimeOut),
                        $"Timed out after {Options.TimeOut}ms waiting for remote process {Process.Id}");

                    FileInfo exceptionFileInfo = new FileInfo(Options.ExceptionFile);
                    if (exceptionFileInfo.Exists && exceptionFileInfo.Length != 0)
                    {
                        throw new RemoteExecutionException(File.ReadAllText(Options.ExceptionFile));
                    }

                    if (Options.CheckExitCode)
                    {
                        int expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Options.ExpectedExitCode : unchecked((sbyte)Options.ExpectedExitCode);
                        int actual = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Process.ExitCode : unchecked((sbyte)Process.ExitCode);

                        Assert.True(expected == actual, $"Exit code was {Process.ExitCode} but it should have been {Options.ExpectedExitCode}");
                    }
                }
                finally
                {
                    if (File.Exists(Options.ExceptionFile))
                    {
                        File.Delete(Options.ExceptionFile);
                    }

                    // Cleanup
                    try { Process.Kill(); }
                    catch { } // ignore all cleanup errors

                    Process.Dispose();
                    Process = null;
                }
            }
        }

        ~RemoteInvokeHandle()
        {
            // Finalizer flags tests that omitted the explicit Dispose() call; they must have it, or they aren't
            // waiting on the remote execution
            Dispose(disposing: false);
        }
    }
}
