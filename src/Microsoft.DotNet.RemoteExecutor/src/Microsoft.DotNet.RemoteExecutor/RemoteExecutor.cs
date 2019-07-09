// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.RemoteExecutor
{
    public static partial class RemoteExecutor
    {
        // A timeout (milliseconds) after which a wait on a remote operation should be considered a failure.
        public const int FailWaitTimeoutMilliseconds = 60 * 1000;
        // The exit code returned when the test process exits successfully.
        public const int SuccessExitCode = 42;
        // The path of the remote executor.
        public static readonly string Path;
        // The name of the host
        public static string HostRunnerName;
        // The path of the host
        public static readonly string HostRunner;
        // Optional additional arguments
        private static readonly string s_extraParameter;

        static RemoteExecutor()
        {
            string processFileName = Process.GetCurrentProcess().MainModule.FileName;
            HostRunnerName = System.IO.Path.GetFileName(processFileName);

            if (PlatformDetection.IsInAppContainer)
            {
                // Host is required to have a remote execution feature integrated. Currently applies to uap and *aot.
                Path = processFileName;
                HostRunner = HostRunnerName;
                s_extraParameter = "remote";
            }
            else if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase))
            {
                Path = System.IO.Path.Combine(AppContext.BaseDirectory, "Microsoft.DotNet.RemoteExecutorHost.dll");
                HostRunner = processFileName;
                s_extraParameter = Path;

                string runtimeConfigPath = GetInnerRuntimeConfig();
                if (runtimeConfigPath != null)
                {
                    s_extraParameter = $"exec --runtimeconfig \"{runtimeConfigPath}\" \"{s_extraParameter}\"";
                }
            }
            else if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                Path = System.IO.Path.Combine(AppContext.BaseDirectory, "Microsoft.DotNet.RemoteExecutorHost.exe");
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
        public static RemoteInvokeHandle Invoke(Func<int> method, RemoteInvokeOptions options = null)
        {
            return Invoke(GetMethodInfo(method), Array.Empty<string>(), options);
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

            // Verify the specified method returns an int (the exit code) or nothing,
            // and that if it accepts any arguments, they're all strings.
            Assert.True(method.ReturnType == typeof(void) || method.ReturnType == typeof(int) || method.ReturnType == typeof(Task<int>));
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

        private static string GetInnerRuntimeConfig()
        {
            const string RuntimeConfigExtension = ".runtimeconfig.json";
            var currentAssembly = typeof(RemoteExecutor).Assembly;

            // We deep-dive into the loaded assemblies and search for the most inner runtimeconfig.json.
            // We need to check for null as global methods in a module don't belong to a type.
            return new StackTrace().GetFrames()
                .Select(frame => frame.GetMethod()?.ReflectedType?.Assembly)
                .Where(asm => asm != null && asm != currentAssembly)
                .Distinct()
                .Select(asm => System.IO.Path.Combine(AppContext.BaseDirectory, asm.GetName().Name + RuntimeConfigExtension))
                .Where(File.Exists)
                .FirstOrDefault();
        }
    }
}
