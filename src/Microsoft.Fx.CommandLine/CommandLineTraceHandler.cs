// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Fx.CommandLine
{
    /// <summary>
    /// Various libraries and tools in CoreFxTools rely on Trace.Trace(Warning|Error) for error reporting.
    /// Enable this handler to log them to the console and set a non-zero exit code if an error is reported.
    /// </summary>
    public static class CommandLineTraceHandler
    {
        private static TraceListener[] _listeners = new TraceListener[]
        {
            new ConsoleTraceListener  { Filter = new EventTypeFilter(SourceLevels.Error | SourceLevels.Warning) },
            new ExitCodeTraceListener { Filter = new EventTypeFilter(SourceLevels.Error) },
        };

        public static void Enable()
        {
            foreach (var listener in _listeners)
            {
                if (!Trace.Listeners.Contains(listener))
                {
                    Trace.Listeners.Add(listener);
                }
            }
        }

        public static void Disable()
        {
            foreach (var listener in _listeners)
            {
                Trace.Listeners.Remove(listener);
            }
        }

        private sealed class ExitCodeTraceListener : TraceListener
        {
            public override void Write(string message)
            {
                Environment.ExitCode = 1;
            }

            public override void WriteLine(string message)
            {
                Write(message);
            }
        }

    }
}
