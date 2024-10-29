// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.Pkg
{
    public static class ExecuteHelper
    {
        public static string Run(string command, string arguments = "")
        {
            if (string.IsNullOrEmpty(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            string output = string.Empty;

            ProcessStartInfo processStartInfo = CreateProcessStartInfo(command, arguments);
            using (Process process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
            }
            return output;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string command, string arguments) =>
            new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
    }
}
