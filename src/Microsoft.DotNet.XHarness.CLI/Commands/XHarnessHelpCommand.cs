// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.Commands.Apple;
using Microsoft.DotNet.XHarness.CLI.Commands.Apple.Simulators;
using Microsoft.DotNet.XHarness.CLI.Commands.Wasm;
using Microsoft.DotNet.XHarness.CLI.Commands.Wasi;
using Microsoft.DotNet.XHarness.Common.CLI;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands;

internal class XHarnessHelpCommand : HelpCommand
{
    public override int Invoke(IEnumerable<string> arguments)
    {
        string[] args = arguments.ToArray();

        if (args.Length == 0)
        {
            base.Invoke(arguments);
            return (int)ExitCode.HELP_SHOWN;
        }

        var command = args[0].ToLowerInvariant();

        string? subCommand = null;
        if (args.Length >= 2)
        {
            subCommand = args[1];
        }

        // Unfortunately, CommandSet.NestedCommandSets is not visible and we cannot go through
        // the command tree dynamically to any depth.
        switch (command)
        {
            case "android":
                PrintCommandHelp(new AndroidCommandSet(), subCommand);
                break;
            case "apple":
#if !DEBUG
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
#endif
                var simulatorsSubset = new SimulatorsCommandSet();
                if (subCommand == simulatorsSubset.Suite)
                {
                    PrintCommandHelp(simulatorsSubset, args.ElementAtOrDefault(2));
                }
                else
                {
                    PrintCommandHelp(new AppleCommandSet(), subCommand);
                }
#if !DEBUG
                    }
                    else
                    {
                        Console.WriteLine($"Command '{command}' could be run on OSX only.");
                    }
#endif
                break;
            case "wasm":
                PrintCommandHelp(new WasmCommandSet(), subCommand);
                break;
            case "wasi":
                PrintCommandHelp(new WasiCommandSet(), subCommand);
                break;
            default:
                Console.WriteLine($"No help available for command '{command}'. Allowed commands are 'apple', 'wasm', 'wasi' and 'android'");
                break;
        }

        return (int)ExitCode.HELP_SHOWN;
    }

    private static void PrintCommandHelp(CommandSet commandSet, string? subcommand)
    {
        if (subcommand != null)
        {
            var command = commandSet.Where(c => c.Name == subcommand).FirstOrDefault();
            if (command != null)
            {
                command.Invoke(new string[] { "--help" });
                return;
            }

            Console.WriteLine($"Unknown sub-command '{subcommand}'.{Environment.NewLine}");
        }

        Console.WriteLine("All supported sub-commands:");
        commandSet.Run(new string[] { "help" });
        Console.WriteLine($"{Environment.NewLine}Run 'xharness {commandSet.Suite} {{command}} --help' for more details");
    }
}
