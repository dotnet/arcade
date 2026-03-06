// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.DotNet.RecursiveSigning.Abstractions;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Registry for container handlers.
    /// Queries handlers in registration order to find the first one that can handle a given file.
    /// </summary>
    public sealed class ContainerHandlerRegistry : IContainerHandlerRegistry
    {
        private readonly ConcurrentBag<IContainerHandler> _handlers = new();

        public IContainerHandler? FindHandler(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            // ConcurrentBag enumeration is thread-safe and provides a moment-in-time snapshot.
            var matchingHandlers = _handlers.Where(h => h.CanHandle(filePath)).Take(2).ToArray();

            return matchingHandlers.Length switch
            {
                0 => null,
                1 => matchingHandlers[0],
                _ => throw new InvalidOperationException($"More than one container handler can handle file '{filePath}'."),
            };
        }

        public void RegisterHandler(IContainerHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _handlers.Add(handler);
        }
    }
}
