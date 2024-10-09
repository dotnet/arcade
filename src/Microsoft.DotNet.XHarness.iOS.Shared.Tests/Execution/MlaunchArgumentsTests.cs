// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Execution;

public class MlaunchArgumentsTests
{
    public class CommandLineDataTestSource
    {
        private static readonly string s_listDevFile = "/my/listdev.txt";
        private static readonly string s_listSimFile = "/my/listsim.txt";
        private static readonly string s_xmlOutputType = "XML";

        public static IEnumerable<object[]> CommandLineArgs => new[] {
                new object[] {
                    new MlaunchArgument[] {
                        new ListDevicesArgument (s_listDevFile)
                    },
                    $"--listdev={s_listDevFile}"
                },

                new object[] {
                    new MlaunchArgument[] {
                        new ListSimulatorsArgument (s_listSimFile)
                    },
                    $"--listsim={s_listSimFile}"
                },

                new object[] {
                    new MlaunchArgument[] {
                        new XmlOutputFormatArgument ()
                    },
                    $"--output-format={s_xmlOutputType}"
                },

                new object[] {
                    new MlaunchArgument[] {
                        new ListExtraDataArgument ()
                    },
                    "--list-extra-data"
                },

                new object[] {
                    new MlaunchArgument[] {
                        new DownloadCrashReportToArgument ("/path/with spaces.txt"),
                        new DeviceNameArgument ("Test iPad")
                    },
                    $"\"--download-crash-report-to=/path/with spaces.txt\" --devname \"Test iPad\""
                },

                new object[] {
                    new MlaunchArgument[] {
                        new SetEnvVariableArgument ("SOME_PARAM", "true"),
                        new SetEnvVariableArgument ("NUNIT_LOG_FILE", "/another space/path.txt")
                    },
                    $"-setenv=SOME_PARAM=true \"-setenv=NUNIT_LOG_FILE=/another space/path.txt\""
                },

                new object[] {
                    new MlaunchArgument[] {
                        new ListDevicesArgument (s_listDevFile),
                        new XmlOutputFormatArgument (),
                        new ListExtraDataArgument ()
                    },
                    $"--listdev={s_listDevFile} --output-format={s_xmlOutputType} --list-extra-data"
                },
            };
    };

    [Theory]
    [MemberData(nameof(CommandLineDataTestSource.CommandLineArgs), MemberType = typeof(CommandLineDataTestSource))]
    public void AsCommandLineTest(MlaunchArgument[] args, string expected) => Assert.Equal(expected, new MlaunchArguments(args).AsCommandLine());

    [Fact]
    public void MlaunchArgumentAndProcessManagerTest()
    {
        var oldArgs = new List<string>() {
                "--download-crash-report-to=/path/with spaces.txt",
                "--sdkroot",
                "/path to xcode/spaces",
                "--devname",
                "Premek's iPhone",
            };

        var newArgs = new MlaunchArguments() {
                new DownloadCrashReportToArgument ("/path/with spaces.txt"),
                new SdkRootArgument ("/path to xcode/spaces"),
                new DeviceNameArgument ("Premek's iPhone"),
            };

        var oldWayOfPassingArgs = StringUtils.FormatArguments(oldArgs);
        var newWayOfPassingArgs = newArgs.AsCommandLine();

        Assert.Equal(oldWayOfPassingArgs, newWayOfPassingArgs);
    }

    [Fact]
    public void MlaunchArgumentEqualityTest()
    {
        var arg1 = new DownloadCrashReportToArgument("/path/with spaces.txt");
        var arg2 = new DownloadCrashReportToArgument("/path/with spaces.txt");
        var arg3 = new DownloadCrashReportToArgument("/path/with.txt");

        Assert.Equal(arg1, arg2);
        Assert.NotEqual(arg1, arg3);
    }

    [Fact]
    public void MlaunchArgumentsEqualityTest()
    {
        var args1 = new MlaunchArgument[] {
                new ListDevicesArgument ("foo"),
                new ListSimulatorsArgument ("bar")
            };
        var args2 = new MlaunchArgument[] {
                new ListDevicesArgument ("foo"),
                new ListSimulatorsArgument ("bar")
            };
        var args3 = new MlaunchArgument[] {
                new ListDevicesArgument ("foo"),
                new ListSimulatorsArgument ("xyz")
            };

        Assert.Equal(args1, args2);
        Assert.NotEqual(args1, args3);
    }
}
