// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

internal class TypeFromAssemblyArgument<T> : Argument<IList<(string path, string? type)>>
                    where T : class
{
    private readonly bool _repeatable;

    public TypeFromAssemblyArgument(string prototype, string description, bool repeatable)
        : base(prototype, description, new List<(string path, string? type)>())
    {
        _repeatable = repeatable;
    }

    public IEnumerable<Type> GetLoadedTypes()
    {
        foreach ((string assemblyPath, string? typeName) in Value)
        {
            var extensionAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
            var loadedType = extensionAssembly?.GetTypes().Where(type => type.FullName == typeName).FirstOrDefault();
            if (loadedType is null)
                throw new Exception($"Can't find type named {typeName} in {assemblyPath}");

            yield return loadedType;
        }
    }

    public override void Action(string argumentValue)
    {
        var split = argumentValue.Split(',', 2);
        var file = split[0];
        string? type = split.Length > 1 ? split[1] : null;

        Value.Add((file, type));
    }

    public override void Validate()
    {
        base.Validate();

        if (!_repeatable && Value.Count > 1)
            throw new ArgumentException($"{Prototype} can only be passed once");

        foreach (var (path, type) in Value)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"Empty path to assembly");
            }

            if (!File.Exists(path))
            {
                throw new ArgumentException($"Failed to find the assembly at {path}");
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException($"No type name given with assembly {path}");
            }
        }
    }
}
