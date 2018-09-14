using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Darc
{
    class Program
    {
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<GetCommandLineOptions, AddCommandLineOptions>(args)
                .MapResult(
                    (GetCommandLineOptions opts) => GetOperation(opts),
                    (AddCommandLineOptions opts) => AddOperation(opts),
                    (errs => 1));
        }

        /// <summary>
        /// Implements the 'get' verb
        /// </summary>
        /// <param name="options"></param>
        static int GetOperation(GetCommandLineOptions options)
        {
            ILoggerFactory loggerFactory = new LoggerFactory().AddConsole();
            ILogger logger = loggerFactory.CreateLogger<Program>();
            Local local = new Local(options.LocalDirectory, logger);
            var allDependencies = local.GetDependencies().Result;
            foreach (var dependency in allDependencies)
            {
                Console.WriteLine($"{dependency.Name} {dependency.Version}");
            }
            return 0;
        }

        static int AddOperation(AddCommandLineOptions options)
        {
            throw new NotImplementedException("Add operation not yet implemented");
        }
    }
}
