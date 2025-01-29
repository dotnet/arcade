// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Xunit.ConsoleClient
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // This code must not contain any references to code in any external assembly (or any code that references any code in any
            // other assembly) until AFTER the creation of the AssemblyHelper.
            var consoleLock = new object();
            var internalDiagnosticsMessageSink = DiagnosticMessageSink.ForInternalDiagnostics(consoleLock, args.Contains("-internaldiagnostics"), args.Contains("-nocolor"));

            using (AssemblyHelper.SubscribeResolveForAssembly(typeof(Program), internalDiagnosticsMessageSink))
                 return callEntryPoint(consoleLock, args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int callEntryPoint(object consoleLock , string[] args)
        {
                 return new ConsoleRunner(consoleLock).EntryPoint(args);
        }
    }
}
