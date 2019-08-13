// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Xunit;
using Xunit.Sdk;

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

        /// <summary>
        /// Lock access to working with CLRMD.
        /// </summary>
        /// <remarks>
        /// ClrMD doesn't like attaching to multiple processes concurrently.  If we happen to
        /// hit multiple remote failures concurrently, only dump out one of them.
        /// </remarks>
        private static int s_clrMdLock = 0;

        private void Dispose(bool disposing)
        {
            Assert.True(disposing, $"A test {AssemblyName}!{ClassName}.{MethodName} forgot to Dispose() the result of RemoteInvoke()");

            if (Process != null)
            {
                // A bit unorthodox to do throwing operations in a Dispose, but by doing it here we avoid
                // needing to do this in every derived test and keep each test much simpler.
                try
                {
                    if (!Process.WaitForExit(Options.TimeOut))
                    {
                        var description = new StringBuilder();
                        description.AppendLine($"Timed out after {Options.TimeOut}ms waiting for remote process.");
                        try
                        {
                            description.AppendLine($"\tProcess ID: {Process.Id}");
                            description.AppendLine($"\tHandle: {Process.Handle}");
                            description.AppendLine($"\tName: {Process.ProcessName}");
                            description.AppendLine($"\tMainModule: {Process.MainModule?.FileName}");
                            description.AppendLine($"\tStartTime: {Process.StartTime}");
                            description.AppendLine($"\tTotalProcessorTime: {Process.TotalProcessorTime}");

                            // Attach ClrMD to gather some additional details.
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && // As of Microsoft.Diagnostics.Runtime v1.0.5, process attach only works on Windows.
                                Interlocked.CompareExchange(ref s_clrMdLock, 1, 0) == 0) // Make sure we only attach to one process at a time.
                            {
                                try
                                {
                                    using (DataTarget dt = DataTarget.AttachToProcess(Process.Id, msecTimeout: 20_000)) // arbitrary timeout
                                    {
                                        ClrRuntime runtime = dt.ClrVersions.FirstOrDefault()?.CreateRuntime();
                                        if (runtime != null)
                                        {
                                            // Dump the threads in the remote process.
                                            description.AppendLine("\tThreads:");
                                            foreach (ClrThread thread in runtime.Threads.Where(t => t.IsAlive))
                                            {
                                                string threadKind =
                                                    thread.IsThreadpoolCompletionPort ? "[Thread pool completion port]" :
                                                    thread.IsThreadpoolGate ? "[Thread pool gate]" :
                                                    thread.IsThreadpoolTimer ? "[Thread pool timer]" :
                                                    thread.IsThreadpoolWait ? "[Thread pool wait]" :
                                                    thread.IsThreadpoolWorker ? "[Thread pool worker]" :
                                                    thread.IsFinalizer ? "[Finalizer]" :
                                                    thread.IsGC ? "[GC]" :
                                                    "";

                                                description.AppendLine($"\t\tThread #{thread.ManagedThreadId} (OS 0x{thread.OSThreadId:X}) {threadKind}");
                                                foreach (ClrStackFrame frame in thread.StackTrace)
                                                {
                                                    description.AppendLine($"\t\t\t{frame}");
                                                }
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    Interlocked.Exchange(ref s_clrMdLock, 0);
                                }
                            }
                        }
                        catch { }

                        throw new XunitException(description.ToString());
                    }

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
