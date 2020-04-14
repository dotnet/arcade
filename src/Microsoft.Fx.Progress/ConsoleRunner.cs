// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Fx.Progress
{
    public static class ConsoleRunner
    {
        private static Stopwatch stopwatch;

        public static void Run(Action<IProgressMonitor> operation)
        {
            if (Console.IsOutputRedirected)
                RunWithRedirectedConsole(operation);
            else
                RunWithConsoleWindow(operation);
        }

        private static void RunWithRedirectedConsole(Action<IProgressMonitor> operation)
        {
            var progressReporter = new ProgressReporter();
            progressReporter.TaskChanged += (s, e) => Console.WriteLine(progressReporter.Task);
            progressReporter.DetailsChanged += (s, e) => Console.WriteLine("  {0}", progressReporter.Details);

            stopwatch = Stopwatch.StartNew();
            Run(operation, progressReporter);
            stopwatch.Stop();

            Console.WriteLine("  Done in {0:g}.", stopwatch.Elapsed);
        }

        private static void RunWithConsoleWindow(Action<IProgressMonitor> operation)
        {
            var currentLeft = Console.CursorLeft;
            if (currentLeft > 0)
                Console.WriteLine();

            // If we have no more room in the buffer, double the size.
            if (Console.BufferHeight - Console.CursorTop <= 2)
            {
                Console.SetBufferSize(Console.BufferWidth, Math.Min(Int16.MaxValue, Console.BufferHeight * 2));
            }

            var currentTop = Console.CursorTop;

            var progressReporter = new ProgressReporter();

            var updateHandler = new EventHandler((s, e) => UpdateOutput(currentTop, progressReporter));

            progressReporter.TaskChanged += updateHandler;
            progressReporter.DetailsChanged += updateHandler;
            progressReporter.PercentageCompleteChanged += updateHandler;
            progressReporter.RemainingTimeChanged += updateHandler;

            stopwatch = Stopwatch.StartNew();

            try
            {
                Run(operation, progressReporter);
            }
            finally 
            {
                stopwatch.Stop();

                WriteDone(currentTop, stopwatch.Elapsed, progressReporter.Task, progressReporter.CancellationToken);
            }
        }

        private static void Run(Action<IProgressMonitor> operation, ProgressReporter progressReporter)
        {
            var cts = new CancellationTokenSource();
            var cancelHandler = new ConsoleCancelEventHandler((s, e) =>
            {
                // Signal that we don't want to terminate but instead continue executing.
                // This allows the console runner to clean up it's output.
                e.Cancel = true;

                cts.Cancel();
            });

            Console.CancelKeyPress += cancelHandler;
            try
            {
                using (var progressMonitor = progressReporter.CreateMonitor(cts))
                using (var cancelingProgressMonitor = new CancelingProgressMonitor(progressMonitor))
                using (var throttledProgressMonitor = new ThrottledProgressMonitor(cancelingProgressMonitor, TimeSpan.FromMilliseconds(500)))
                    operation(throttledProgressMonitor);
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        private static void ClearOutput(int top)
        {
            var emptyString = new string(' ', Console.BufferWidth);

            Console.SetCursorPosition(0, top);
            Console.Write(emptyString);

            Console.SetCursorPosition(0, top + 1);
            Console.Write(emptyString);

            Console.SetCursorPosition(0, top + 2);
            Console.Write(emptyString);
        }

        private static void UpdateOutput(int top, ProgressReporter progressReporter)
        {
            ClearOutput(top);

            Console.SetCursorPosition(0, top);
            Console.Write(progressReporter.Task);

            Console.SetCursorPosition(0, top + 1);
            Console.Write("  {0}", progressReporter.Details);

            Console.SetCursorPosition(0, top + 2);
            Console.Write("  {0:P2} - ETA {1:g}, Elapsed {2:g}",
                progressReporter.PercentageComplete,
                progressReporter.RemainingTime,
                stopwatch == null ? TimeSpan.FromMilliseconds(0) : stopwatch.Elapsed);
        }

        private static void WriteDone(int top, TimeSpan elapsed, string task, CancellationToken cancellationToken)
        {
            ClearOutput(top);
            Console.SetCursorPosition(0, top);

            if (task.EndsWith("..."))
                task = task.Substring(0, task.Length - 3);
            else if (task.EndsWith("."))
                task = task.Substring(0, task.Length - 1);

            var doneOrAborted = cancellationToken.IsCancellationRequested ? "Aborted" : "Done";

            Console.WriteLine("{0}. {1} in {2:g}.", task, doneOrAborted, elapsed);
        }

        public static T Run<T>(Func<IProgressMonitor, T> operation)
        {
            var result = default(T);
            var action = new Action<IProgressMonitor>(pm =>
            {
                result = operation(pm);
            });

            Run(action);
            return result;
        }
    }
}
