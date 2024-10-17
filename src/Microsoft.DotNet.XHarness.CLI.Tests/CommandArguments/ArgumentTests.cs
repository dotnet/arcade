// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.Arguments;

public class ArgumentTests
{
    private class SampleRepeatableArgument : RepeatableArgument
    {
        public SampleRepeatableArgument()
            : base("a=", string.Empty)
        {
        }
    }

    [Fact]
    public void RepetableArgumentsAreParsed()
    {
        var arg = new SampleRepeatableArgument();
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(new[]
        {
            "-a",
            "foo",
            "-a=bar",
        });

        Assert.Equal(0, exitCode);
        Assert.True(command.CommandRun);
        Assert.Equal(new[] { "foo", "bar" }, arg.Value);
    }

    private class SampleSwitchArgument : SwitchArgument
    {
        public SampleSwitchArgument(bool defaultValue)
            : base("b:", string.Empty, defaultValue)
        {
        }
    }

    [Fact]
    public void SwitchArgumentArgumentWithoutValueIsTrue()
    {
        var arg = new SampleSwitchArgument(false);
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(new[]
        {
            "-b",
        });

        Assert.Equal(0, exitCode);
        Assert.True(command.CommandRun);
        Assert.True(arg.Value);
    }

    [Fact]
    public void SwitchArgumentArgumentWithTrueDefaultIsFalse()
    {
        var arg = new SampleSwitchArgument(true);
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(new[]
        {
            "-b",
        });

        Assert.Equal(0, exitCode);
        Assert.True(command.CommandRun);
        Assert.False(arg.Value);
    }

    [Fact]
    public void SwitchArgumentArgumentWithValueIsFalse()
    {
        var arg = new SampleSwitchArgument(false);
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(new[]
        {
            "-b=false",
        });

        Assert.Equal(0, exitCode);
        Assert.True(command.CommandRun);
        Assert.False(arg.Value);
    }

    [Fact]
    public void SwitchArgumentArgumentWithDefaultValueIsFalse()
    {
        var arg = new SampleSwitchArgument(true);
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(new[]
        {
            "-b=off",
        });

        Assert.Equal(0, exitCode);
        Assert.True(command.CommandRun);
        Assert.False(arg.Value);
    }

    private class SampleStringArgument : RequiredStringArgument
    {
        public SampleStringArgument()
            : base("c=", string.Empty)
        {
        }
    }

    [Fact]
    public void RequiredStringArgumentIsSet()
    {
        var arg = new SampleStringArgument();
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(new[]
        {
            "-c",
            "xyz",
        });

        Assert.Equal(0, exitCode);
        Assert.True(command.CommandRun);
        Assert.Equal("xyz", arg.Value);
    }

    [Fact]
    public void RequiredStringArgumentIsValidated()
    {
        var arg = new SampleStringArgument();
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(Array.Empty<string>());

        Assert.Equal((int)ExitCode.INVALID_ARGUMENTS, exitCode);
    }

    private class SampleTimeSpanArgument : TimeSpanArgument
    {
        public SampleTimeSpanArgument(TimeSpan defaultValue)
            : base("t=", string.Empty, defaultValue)
        {
        }
    }

    [Fact]
    public void TimeSpanArgumentHasDefault()
    {
        var arg = new SampleTimeSpanArgument(TimeSpan.FromMinutes(3));
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(Array.Empty<string>());

        Assert.Equal(0, exitCode);
        Assert.True(command.CommandRun);
        Assert.Equal(TimeSpan.FromMinutes(3), arg.Value);
    }

    [Fact]
    public void TimeSpanArgumentIsSet()
    {
        var arg = new SampleTimeSpanArgument(TimeSpan.FromMinutes(3));
        var command = UnitTestCommand.FromArgument(arg);

        var exitCode = command.Invoke(new[]
        {
            "-t",
            "00:02:30",
        });

        Assert.Equal(0, exitCode);
        Assert.True(command.CommandRun);
        Assert.Equal(TimeSpan.FromSeconds(150), arg.Value);
    }

    [Fact]
    public void ArgumentsAreInterpolatedWell()
    {
        var timespanArg = new SampleTimeSpanArgument(TimeSpan.FromSeconds(5));
        var switchArg = new SampleSwitchArgument(true);
        var stringArg = new SampleStringArgument();
        stringArg.Action("string-value");

        Assert.Equal("time is 00:00:05", $"time is {timespanArg}");
        Assert.Equal("switch is true", $"switch is {switchArg}");
        Assert.Equal("string is string-value", $"string is {stringArg}");
    }
}
