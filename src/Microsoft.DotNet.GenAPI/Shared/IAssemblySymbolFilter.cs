// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared
{
    /// <summary>
    /// Defines interface for implementing filtering of attributes, namespaces, types, members, filter out/allow private, internals.
    /// </summary>
    public interface IAssemblySymbolFilter
    {
        /// <summary>
        /// Interface for fitlering our corresponding namespace and it's types, members.
        /// </summary>
        /// <param name="ns">Object of <cref="INamespaceSymbol"/>.</param>
        /// <returns>Boolean value.</returns>
        bool Include(INamespaceSymbol ns);

        /// <summary>
        /// Interface for fitlering our corresponding attribute.
        /// </summary>
        /// <param name="ns">Object of <cref="AttributeData"/>.</param>
        /// <returns>Boolean value.</returns>
        bool Include(AttributeData at);

        /// <summary>
        /// Interface for fitlering our corresponding type and it's members .
        /// </summary>
        /// <param name="ns">Object of <cref="ITypeSymbol"/>.</param>
        /// <returns>Boolean value.</returns>
        bool Include(ITypeSymbol ts);

        /// <summary>
        /// Interface for fitlering our corresponding member.
        /// </summary>
        /// <param name="ns">Object of <cref="ISymbol"/>.</param>
        /// <returns>Boolean value.</returns>
        bool Include(ISymbol member);
    }
}
