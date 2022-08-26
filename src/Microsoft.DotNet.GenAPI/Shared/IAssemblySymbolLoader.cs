// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Interface responsible for creating Compilation Factory and loading <see cref="IAssemblySymbol"/> out of binaries.
/// </summary>
public interface IAssemblySymbolLoader
{
    /// <summary>
    /// Loads an assembly from the provided path.
    /// </summary>
    /// <param name="path">The full path to the assembly.</param>
    /// <returns><see cref="IAssemblySymbol"/> representing the loaded assembly.</returns>
    IAssemblySymbol? LoadAssembly(string path);

    /// <summary>
    /// Loads an assembly from a given <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The stream to read the metadata from.</param>
    /// <returns><see cref="IAssemblySymbol"/> respresenting the given <paramref name="stream"/>.</returns>
    IAssemblySymbol? LoadAssembly(Stream stream);
}
