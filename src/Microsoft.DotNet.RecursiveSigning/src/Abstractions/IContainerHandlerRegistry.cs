// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Registry for discovering and routing to appropriate container handlers.
    /// </summary>
    public interface IContainerHandlerRegistry
    {
        /// <summary>
        /// Find a handler that can process the given file.
        /// </summary>
        /// <param name="filePath">File path to check.</param>
        /// <returns>Handler if found, null otherwise.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when more than one registered handler reports it can handle <paramref name="filePath" />.
        /// </exception>
        IContainerHandler? FindHandler(string filePath);

        /// <summary>
        /// Register a container handler.
        /// </summary>
        /// <param name="handler">Handler to register.</param>
        void RegisterHandler(IContainerHandler handler);
    }
}
