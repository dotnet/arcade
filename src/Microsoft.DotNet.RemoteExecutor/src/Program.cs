// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;

namespace Microsoft.DotNet.RemoteExecutor
{
    /// <summary>
    /// Provides an entry point in a new process that will load a specified method and invoke it.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // The program expects to be passed the target assembly name to load, the type
            // from that assembly to find, and the method from that assembly to invoke.
            // Any additional arguments are passed as strings to the method.
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: {0} assemblyName typeName methodName exceptionFile [additionalArgType additionalArg]...", typeof(Program).GetTypeInfo().Assembly.GetName().Name);
                Environment.Exit(-1);
                return -1;
            }

            string assemblyName = args[0];
            string typeName = args[1];
            string methodName = args[2];
            string exceptionFile = args[3];
            ReadOnlySpan<string> additionalArgs = args.Length > 4 ?
                args.AsSpan(4, args.Length - 4)
                : ReadOnlySpan<string>.Empty;

            object[] actualArgs  = new object[additionalArgs.Length / 2];
            for (int i = 0; i < actualArgs.Length; i++)
            {
                string typeString = additionalArgs[i * 2];
                string value = additionalArgs[i * 2 + 1];
                actualArgs[i] = ConvertStringToArgument(typeString, value);
            }

            // Load the specified assembly, type, and method, then invoke the method.
            // The program's exit code is the return value of the invoked method.
            Assembly a = null;
            Type t = null;
            MethodInfo mi = null;
            object instance = null;
            int exitCode = 0;
            try
            {
                // Create the test class if necessary
                a = Assembly.Load(new AssemblyName(assemblyName));
                t = a.GetType(typeName);
                mi = t.GetTypeInfo().GetDeclaredMethod(methodName);
                if (!mi.IsStatic)
                {
                    instance = Activator.CreateInstance(t);
                }

                // Invoke the test
                object result = mi.Invoke(instance, actualArgs);

                if (result is Task<int> task)
                {
                    exitCode = task.GetAwaiter().GetResult();
                }
                else if (result is Task resultValueTask)
                {
                    resultValueTask.GetAwaiter().GetResult();
                }
                else if (result is int exit)
                {
                    exitCode = exit;
                }
            }
            catch (Exception exc)
            {
                if (exc is TargetInvocationException && exc.InnerException != null)
                    exc = exc.InnerException;

                var output = new StringBuilder();
                output.AppendLine();
                output.AppendLine("Child exception:");
                output.AppendLine("  " + exc);
                output.AppendLine();
                output.AppendLine("Child process:");
                output.AppendLine(string.Format("  {0} {1} {2}", a, t, mi));
                output.AppendLine();

                if (actualArgs.Length > 0)
                {
                    output.AppendLine("Child arguments:");
                    output.AppendLine("  " + string.Join(", ", actualArgs));
                }

                File.WriteAllText(exceptionFile, output.ToString());

                ExceptionDispatchInfo.Capture(exc).Throw();
            }
            finally
            {
                (instance as IDisposable)?.Dispose();
            }

            // Use Exit rather than simply returning the exit code so that we forcibly shut down
            // the process even if there are foreground threads created by the operation that would
            // end up keeping the process alive potentially indefinitely.
            try
            {
                Environment.Exit(exitCode);
            }
            catch (PlatformNotSupportedException)
            {
            }

            return exitCode;
        }

        private static string UnescapeString(string value)
        {
            var stringBuilder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (i < value.Length - 2 && value[i] == '$' && value[i + 1] == '\\')
                {
                    // Unescape this char. E.g. "$\\0" -> '\0'
                    char nextChar = value[i + 2];
                    if (nextChar == '0')
                    {
                        stringBuilder.Append('\0');
                    }
                    else
                    {
                        // Unknown escaped char.
                        stringBuilder.Append('$');
                        stringBuilder.Append('\\');
                        stringBuilder.Append(nextChar);
                    }

                    i+= 2;
                }
                else
                {
                    stringBuilder.Append(value[i]);
                }
            }

            return stringBuilder.ToString();
        }

        private static object ConvertStringToArgument(string typeString, string value)
        {
            if (typeString == "(null)")
            {
                return null;
            }

            Type type = Type.GetType(typeString);
            if (type == null)
            {
                throw new Exception($"Can't create type \"{type.FullName}\"");
            }
            
            if (type.IsEnum)
            {
                return Enum.Parse(type, value);
            }
            else if (type == typeof(string))
            {
                return UnescapeString(value);
            }
            else if (type == typeof(char))
            {
                return UnescapeString(value)[0];
            }
            
            return Convert.ChangeType(value, type);
        }
    }
}
