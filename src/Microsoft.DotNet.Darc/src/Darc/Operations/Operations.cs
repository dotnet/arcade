// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Models;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.DotNet.Darc
{
    internal static class Operations
    {
        /// <summary>
        /// Implements the 'authenticate' verb
        /// </summary>
        /// <param name="options"></param>
        public static int AuthenticateOperation(AuthenticateCommandLineOptions options)
        {
            ILogger logger = GetLogger(options);

            AuthenticateEditorPopUp initEditorPopUp = new AuthenticateEditorPopUp("authenticate-settings/authenticate-todo", logger);

            UxManager uxManager = new UxManager(logger);

            return uxManager.PopUp(initEditorPopUp);
        }

        /// <summary>
        /// Implements the 'get' verb
        /// </summary>
        /// <param name="options"></param>
        public static int GetOperation(GetCommandLineOptions options)
        {
            Local local = new Local(options.LocalDirectory, GetLogger(options));
            var allDependencies = local.GetDependencies().Result;
            foreach (var dependency in allDependencies)
            {
                Console.WriteLine($"{dependency.Name} {dependency.Version} from {dependency.RepoUri}@{dependency.Commit}");
            }
            return 0;
        }

        public static int AddOperation(AddCommandLineOptions options)
        {
            throw new NotImplementedException("Add operation not yet implemented");
        }

        /// <summary>
        /// Retrieve the console logger for the CLI.
        /// </summary>
        /// <returns>New logger</returns>
        /// <remarks>Because the internal logging in DarcLib tends to be chatty and non-useful,
        /// we remap the --verbose switch onto 'info', --debug onto highest level, and the default level onto warning</remarks>
        private static ILogger GetLogger(CommandLineOptions options)
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
    }
}
