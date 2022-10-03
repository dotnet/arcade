// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Interface responsible for writing various outputs: C# code, XML etc.
/// </summary>
public interface ISyntaxWriter : IDisposable
{
    /// <summary>
    /// Writes namespace in C#, XML formats.
    /// </summary>
    /// <param name="namespacePath">List of nested namespaces: { parent, child }. Empty list for global namespace. </param>
    /// <returns>Returns disposable object. `obj.Dispose()` is called when current namespace is completely processed.</returns>
    IDisposable WriteNamespace(IEnumerable<string> namespacePath);

    /// <summary>
    /// Writes type definition with corresponding accessibility, base types and constraints.
    /// </summary>
    /// <param name="accessibility">List of accessibility: public, private, protected, internal, etc.</param>
    /// <param name="typeName">Type name without namespaces</param>
    /// <param name="keywords">List of keywords: struct, partial, ref, class, readonly, etc.</param>
    /// <param name="baseTypeNames">List of interfaces, base classes</param>
    /// <param name="constraints">List of constraint  for generic type parameters.</param>
    /// <returns>Returns disposable object. `obj.Dispose()` is called when current type definition is completely processed.</returns>
    IDisposable WriteTypeDefinition(
        IEnumerable<SyntaxKind> accessibility,
        IEnumerable<SyntaxKind> keywords,
        string typeName,
        IEnumerable<string> baseTypeNames,
        IEnumerable<IEnumerable<SymbolDisplayPart>> constraints);

    /// <summary>
    /// Writes attribute data.
    /// </summary>
    /// <param name="attribute">String representation of attribute.</param>
    void WriteAttribute(string attribute);

    /// <summary>
    /// Writes property symbol.
    /// </summary>
    /// <param name="definition">Includes property type and name. `bool Field`</param>
    /// <param name="isAbstract">If poperty is abstract - implementation should be ommited.</param>
    /// <param name="hasGetMethod">If `get` method specified.</param>
    /// <param name="hasSetMethod">If `set` method specified.</param>
    void WriteProperty(string definition, bool isAbstract, bool hasGetMethod, bool hasSetMethod);

    /// <summary>
    /// Writes event symbol.
    /// </summary>
    /// <param name="definition">Includes type and name.</param>
    /// <param name="hasAddMethod">If `has` method is specified.</param>
    /// <param name="hasRemoveMethod">If `remove` method is specified.</param>
    void WriteEvent(string definition, bool hasAddMethod, bool hasRemoveMethod);

    /// <summary>
    /// Writes method symbol.
    /// </summary>
    /// <param name="definition">Includes return type, name and parameters with default values.</param>
    /// <param name="isAbstract">If method is abstract - implementation should be ommited.</param>
    void WriteMethod(string definition, bool isAbstract);

    /// <summary>
    /// Writes field symbols like enum name = value.
    /// </summary>
    /// <param name="definition">Name and constant value (`GotoStatements = 4,`).</param>
    void WriteField(string definition);
}
