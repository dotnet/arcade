using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands;

internal class XHarnessVersionCommand : Command
{
    public XHarnessVersionCommand() : base("version") { }

    public override int Invoke(IEnumerable<string> arguments)
    {
        var version = GetAssemblyVersion();

        if (!arguments.Contains("-v"))
        {
            Console.WriteLine(version.ProductVersion);
            return 0;
        }
        
        // Print the name of the tool and the version number unix style
        // Example:
        // Apple clang version 11.0.3 (clang-1103.0.32.29)
        // Target: x86_64-apple-darwin19.4.0
        // InstalledDir: /Applications/Xcode114.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/bin
        Console.WriteLine($"XHarness version {version.ProductVersion} ({version.OriginalFilename})");
        Console.WriteLine($"InstalledDir: {version.FileName}");
        return 0;
    }

    public static FileVersionInfo GetAssemblyVersion() => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
}
