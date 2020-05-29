// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using Microsoft.DotNet.XUnitExtensions;

namespace Microsoft.DotNet.RemoteExecutor
{
    public static partial class RemoteExecutor
    {
        /// <summary>
        /// A timeout (milliseconds) after which a wait on a remote operation should be considered a failure.
        /// </summary>
        public const int FailWaitTimeoutMilliseconds = 60 * 1000;

        /// <summary>
        /// The exit code returned when the test process exits successfully.
        /// </summary>
        public const int SuccessExitCode = 42;

        /// <summary>
        /// The path of the remote executor.
        /// </summary>
        public static readonly string Path;

        /// <summary>
        /// The name of the host.
        /// </summary>
        public static string HostRunnerName;

        /// <summary>
        /// The path of the host.
        /// </summary>
        public static readonly string HostRunner;

        /// <summary>
        /// Optional additional arguments.
        /// </summary>
        private static readonly string s_extraParameter;

        static RemoteExecutor()
        {
            string processFileName = Process.GetCurrentProcess().MainModule?.FileName;
            if (processFileName == null)
            {
                return;
            }
            HostRunnerName = System.IO.Path.GetFileName(processFileName);

            if (PlatformDetection.IsInAppContainer)
            {
                // Host is required to have a remote execution feature integrated, i.e. UWP.
                Path = processFileName;
                HostRunner = HostRunnerName;
                s_extraParameter = "remote";
            }
            else if (Environment.Version.Major >= 5 || RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase))
            {
                Path = typeof(RemoteExecutor).Assembly.Location;
                HostRunner = processFileName;
                s_extraParameter = "exec";

                (string runtimeConfigPath, string depsJsonPath) = GetAppRuntimeOptions();

                if (runtimeConfigPath != null)
                {
                    s_extraParameter += $" --runtimeconfig \"{runtimeConfigPath}\"";
                }

                if (depsJsonPath != null)
                {
                    s_extraParameter += $" --depsfile \"{depsJsonPath}\"";
                }

                s_extraParameter += $" \"{Path}\"";
            }
            else if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                Path = typeof(RemoteExecutor).Assembly.Location;
                HostRunner = Path;
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Action method, RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), Array.Empty<string>(), options);
        }


        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Action<string> method, string arg, RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), new[] { arg }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Action<string, string> method, string arg1, string arg2,
            RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), new[] { arg1, arg2 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Action<string, string, string> method, string arg1, string arg2,
            string arg3, RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), new[] { arg1, arg2, arg3 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Action<string, string, string, string> method, string arg1,
            string arg2, string arg3, string arg4, RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), new[] { arg1, arg2, arg3, arg4 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<Task<int>> method, RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), Array.Empty<string>(), options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg">The argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, Task<int>> method, string arg,
            RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { arg }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg1">The first argument to pass to the method.</param>
        /// <param name="arg2">The second argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, string, Task<int>> method, string arg1, string arg2,
            RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { arg1, arg2 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg1">The first argument to pass to the method.</param>
        /// <param name="arg2">The second argument to pass to the method.</param>
        /// <param name="arg3">The third argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, string, string, Task<int>> method, string arg1,
            string arg2, string arg3, RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { arg1, arg2, arg3 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<Task> method, RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), Array.Empty<string>(), options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg">The argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, Task> method, string arg,
            RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), new[] { arg }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg1">The first argument to pass to the method.</param>
        /// <param name="arg2">The second argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, string, Task> method, string arg1, string arg2,
            RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), new[] { arg1, arg2 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg1">The first argument to pass to the method.</param>
        /// <param name="arg2">The second argument to pass to the method.</param>
        /// <param name="arg3">The third argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, string, string, Task> method, string arg1,
            string arg2, string arg3, RemoteInvokeOptions options = null)
        {
            // There's no exit code to check
            options = options ?? new RemoteInvokeOptions();
            options.CheckExitCode = false;

            return Invoke(GetMethodInfo(method), new[] { arg1, arg2, arg3 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<int> method, RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), Array.Empty<string>(), options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg">The argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, int> method, string arg,
            RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { arg }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg1">The first argument to pass to the method.</param>
        /// <param name="arg2">The second argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, string, int> method, string arg1, string arg2,
            RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { arg1, arg2 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg1">The first argument to pass to the method.</param>
        /// <param name="arg2">The second argument to pass to the method.</param>
        /// <param name="arg3">The third argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, string, string, int> method, string arg1,
            string arg2, string arg3, RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { arg1, arg2, arg3 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg1">The first argument to pass to the method.</param>
        /// <param name="arg2">The second argument to pass to the method.</param>
        /// <param name="arg3">The third argument to pass to the method.</param>
        /// <param name="arg4">The fourth argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, string, string, string, int> method, string arg1,
            string arg2, string arg3, string arg4, RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { arg1, arg2, arg3, arg4 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="arg1">The first argument to pass to the method.</param>
        /// <param name="arg2">The second argument to pass to the method.</param>
        /// <param name="arg3">The third argument to pass to the method.</param>
        /// <param name="arg4">The fourth argument to pass to the method.</param>
        /// <param name="arg5">The fifth argument to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle Invoke(Func<string, string, string, string, string, int> method,
            string arg1, string arg2, string arg3, string arg4, string arg5, RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { arg1, arg2, arg3, arg4, arg5 }, options);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments without performing any modifications to the arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="unparsedArg">The arguments to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        public static RemoteInvokeHandle InvokeRaw(Delegate method, string unparsedArg,
            RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), new[] { unparsedArg }, options, pasteArguments: false);
        }

        /// <summary>Invokes the method from this assembly in another process using the specified arguments.</summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="args">The arguments to pass to the method.</param>
        /// <param name="options">Options to use for the invocation.</param>
        /// <param name="pasteArguments">true if this function should paste the arguments (e.g. surrounding with quotes); false if that responsibility is left up to the caller.</param>
        private static RemoteInvokeHandle Invoke(MethodInfo method, string[] args,
            RemoteInvokeOptions options, bool pasteArguments = true)
        {
            options = options ?? new RemoteInvokeOptions();

            // For platforms that do not support RemoteExecutor
            if (options.ShouldSkipInvocation)
            {
                throw new SkipTestException("RemoteExecutor is not supported on this platform.");
            }

            // Verify the specified method returns an int (the exit code) or nothing,
            // and that if it accepts any arguments, they're all strings.
            Assert.True(method.ReturnType == typeof(void)
                || method.ReturnType == typeof(int)
                || method.ReturnType == typeof(Task)
                || method.ReturnType == typeof(Task<int>));
            Assert.All(method.GetParameters(), pi => Assert.Equal(typeof(string), pi.ParameterType));

            // And make sure it's in this assembly.  This isn't critical, but it helps with deployment to know
            // that the method to invoke is available because we're already running in this assembly.
            Type t = method.DeclaringType;
            Assembly a = t.GetTypeInfo().Assembly;

            // Start the other process and return a wrapper for it to handle its lifetime and exit checking.
            ProcessStartInfo psi = options.StartInfo;
            psi.UseShellExecute = false;

            if (!options.EnableProfiling)
            {
                // Profilers / code coverage tools doing coverage of the test process set environment
                // variables to tell the targeted process what profiler to load.  We don't want the child process 
                // to be profiled / have code coverage, so we remove these environment variables for that process 
                // before it's started.
                psi.Environment.Remove("Cor_Profiler");
                psi.Environment.Remove("Cor_Enable_Profiling");
                psi.Environment.Remove("CoreClr_Profiler");
                psi.Environment.Remove("CoreClr_Enable_Profiling");
            }

            // If we need the host (if it exists), use it, otherwise target the console app directly.
            string metadataArgs = PasteArguments.Paste(new string[] { a.FullName, t.FullName, method.Name, options.ExceptionFile }, pasteFirstArgumentUsingArgV0Rules: false);
            string passedArgs = pasteArguments ? PasteArguments.Paste(args, pasteFirstArgumentUsingArgV0Rules: false) : string.Join(" ", args);
            string testConsoleAppArgs = s_extraParameter + " " + metadataArgs + " " + passedArgs;

            if (options.RunAsSudo)
            {
                psi.FileName = "sudo";
                psi.Arguments = HostRunner + " " + testConsoleAppArgs;

                // Create exception file up front so there are no permission issue when RemoteInvokeHandle tries to delete it.
                File.WriteAllText(options.ExceptionFile, "");
            }
            else
            {
                psi.FileName = HostRunner;
                psi.Arguments = testConsoleAppArgs;
            }

            // Return the handle to the process, which may or not be started
            return new RemoteInvokeHandle(options.Start ? Process.Start(psi) : new Process() { StartInfo = psi },
                options, a.FullName, t.FullName, method.Name);
        }

        private static MethodInfo GetMethodInfo(Delegate d)
        {
            // RemoteInvoke doesn't support marshaling state on classes associated with
            // the delegate supplied (often a display class of a lambda).  If such fields
            // are used, odd errors result, e.g. NullReferenceExceptions during the remote
            // execution.  Try to ward off the common cases by proactively failing early
            // if it looks like such fields are needed.
            if (d.Target != null)
            {
                // The only fields on the type should be compiler-defined (any fields of the compiler's own
                // making generally include '<' and '>', as those are invalid in C# source).  Note that this logic
                // may need to be revised in the future as the compiler changes, as this relies on the specifics of
                // actually how the compiler handles lifted fields for lambdas.
                Type targetType = d.Target.GetType();
                Assert.All(
                    targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                    fi => Assert.True(fi.Name.IndexOf('<') != -1, $"Field marshaling is not supported by {nameof(Invoke)}: {fi.Name}"));
            }

            return d.GetMethodInfo();
        }

        private static (string runtimeConfigPath, string depsJsonPath) GetAppRuntimeOptions()
        {
            Assembly currentAssembly = typeof(RemoteExecutor).Assembly;

            // We deep-dive into the loaded assemblies and search for the most inner runtimeconfig.json.
            // We need to check for null as global methods in a module don't belong to a type.
            IEnumerable<Assembly> assemblies = new StackTrace().GetFrames()
                .Select(frame => frame.GetMethod()?.ReflectedType?.Assembly)
                .Where(asm => asm != null && asm != currentAssembly)
                .Distinct();

            string runtimeConfigPath = assemblies
                .Select(asm => System.IO.Path.Combine(AppContext.BaseDirectory, asm.GetName().Name + ".runtimeconfig.json"))
                .Where(File.Exists)
                .FirstOrDefault();

            string depsJsonPath = assemblies
                .Select(asm => System.IO.Path.Combine(AppContext.BaseDirectory, asm.GetName().Name + ".deps.json"))
                .Where(File.Exists)
                .FirstOrDefault();
            
            return (runtimeConfigPath, depsJsonPath);
        }
    }
}
