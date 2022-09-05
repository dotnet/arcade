// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

public interface ISyntaxWriter : IDisposable
{
    IDisposable WriteNamespace(IEnumerable<string> namespacePath);

    IDisposable WriteTypeDefinition(IEnumerable<SyntaxKind> accessibility, IEnumerable<SyntaxKind> keywords,
        string typeName, IEnumerable<string> baseTypeNames, IEnumerable<IEnumerable<SymbolDisplayPart>> constrains);

    void WriteAttribute(string attribute);

    void WriteProperty(string definition, bool hasImplementation, bool hasGetMethod, bool hasSetMethod);

    void WriteEvent(string definition, bool hasAddMethod, bool hasRemoveMethod);

    void Writemethod(string definition, bool hasImplementation);
}
