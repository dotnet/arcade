// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.XHarness.Common.Utilities;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Execution;

// mlaunch is really important and has a lot of arguments that are known but
// used to be passed as strings. This class allows to add arguments without
// knowing the exact string and will also validate that an argument that
// needs a value does contain the value
public abstract class MlaunchArgument
{
    public abstract string AsCommandLineArgument();

    protected static string Escape(string value) => StringUtils.FormatArguments(value);

    public override bool Equals(object obj) => obj is MlaunchArgument arg && arg.AsCommandLineArgument() == AsCommandLineArgument();

    public override int GetHashCode() => AsCommandLineArgument().GetHashCode();
}

public abstract class SingleValueArgument : MlaunchArgument
{
    private readonly string _argumentName;
    private readonly string _argumentValue;
    private readonly bool _useEqualSign;

    protected SingleValueArgument(string argumentName, string argumentValue, bool useEqualSign = true)
    {
        _argumentName = argumentName ?? throw new ArgumentNullException(nameof(argumentName));
        _argumentValue = argumentValue ?? throw new ArgumentNullException(nameof(argumentValue));
        _useEqualSign = useEqualSign;
    }

    public override string AsCommandLineArgument()
    {
        if (_useEqualSign)
        {
            return Escape($"--{_argumentName}={_argumentValue}");
        }
        else
        {
            return $"--{_argumentName} {Escape(_argumentValue)}";
        }
    }
}

public abstract class OptionArgument : MlaunchArgument
{
    private readonly string _argumentName;

    protected OptionArgument(string argumentName)
    {
        _argumentName = argumentName ?? throw new ArgumentNullException(nameof(argumentName));
    }

    public override string AsCommandLineArgument() => $"--{_argumentName}";
}

public class MlaunchArguments : IEnumerable<MlaunchArgument>
{
    private readonly List<MlaunchArgument> _arguments = new();

    public MlaunchArguments(params MlaunchArgument[] args)
    {
        _arguments.AddRange(args);
    }

    public void Add(MlaunchArgument arg) => _arguments.Add(arg);

    public void AddRange(IEnumerable<MlaunchArgument> args) => _arguments.AddRange(args);

    public string AsCommandLine() => string.Join(" ", _arguments.Select(a => a.AsCommandLineArgument()));

    public IEnumerator<MlaunchArgument> GetEnumerator() => _arguments.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _arguments.GetEnumerator();

    public override string ToString() => AsCommandLine();

    public override bool Equals(object obj) => obj is MlaunchArguments arg && arg.AsCommandLine() == AsCommandLine();

    public override int GetHashCode() => AsCommandLine().GetHashCode();
}
