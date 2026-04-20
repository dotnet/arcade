// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Reporter
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                ReporterOptions options = ReporterOptions.Parse(args);
                if (options.ShowHelp)
                {
                    return 0;
                }

                ReporterRunner runner = new ReporterRunner(options);
                return await runner.RunAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
