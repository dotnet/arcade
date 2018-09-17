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
        /// Retrieve the console logger for the CLI.
        /// </summary>
        /// <returns>New logger</returns>
        /// <remarks>Because the internal logging in DarcLib tends to be chatty and non-useful,
        /// we remap the --verbose switch onto 'info', --debug onto highest level, and the default level onto warning</remarks>
        static private ILogger GetLogger(CommandLineOptions options)
        {
            LogLevel level = LogLevel.Warning;
            if (options.Debug)
            {
                level = LogLevel.Debug;
            }
            else if (options.Verbose)
            {
                level = LogLevel.Information;
            }
            ILoggerFactory loggerFactory = new LoggerFactory().AddConsole(level);
            return loggerFactory.CreateLogger<Program>();
        }

        /// <summary>
        /// Implements the 'get' verb
        /// </summary>
        /// <param name="options"></param>
        static int GetOperation(GetCommandLineOptions options)
        {
            Local local = new Local(options.LocalDirectory, GetLogger(options));
            var allDependencies = local.GetDependencies().Result;
            foreach (var dependency in allDependencies)
            {
                Console.WriteLine($"{dependency.Name} {dependency.Version} from {dependency.RepoUri}@{dependency.Commit}");
            }
            return 0;
        }

        static int AddOperation(AddCommandLineOptions options)
        {
            throw new NotImplementedException("Add operation not yet implemented");
        }
    }
}
