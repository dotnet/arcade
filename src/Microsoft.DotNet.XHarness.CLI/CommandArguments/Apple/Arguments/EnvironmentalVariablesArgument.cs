// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// Environmental variables set when executing the application.
/// </summary>
internal class EnvironmentalVariablesArgument : Argument
{
    public IReadOnlyCollection<(string, string)> Value => _environmentalVariables;
    private readonly List<(string, string)> _environmentalVariables = new();

    public EnvironmentalVariablesArgument() : base("set-env=", "Environmental variable to set for the application in format key=value. Can be used multiple times")
    {
    }

    public override void Action(string argumentValue)
    {
        var position = argumentValue.IndexOf('=');
        if (position == -1)
        {
            throw new ArgumentException($"The set-env argument {argumentValue} must be in the key=value format");
        }

        var key = argumentValue.Substring(0, position);
        var value = argumentValue.Substring(position + 1);
        _environmentalVariables.Add((key, value));
    }
}
