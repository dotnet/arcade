// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

public class CSharpSyntaxWriter: ISyntaxWriter
{
    private readonly IOutWriter _outWriter;

    public CSharpSyntaxWriter(IOutWriter outWriter) => _outWriter = outWriter;

    public IDisposable WriteNamespace(IEnumerable<string> namespacePath)
    {
        WriteKeyword(SyntaxKind.NamespaceKeyword);

        bool root = true;
        foreach (var ns in namespacePath)
        {
            if (!root)
                WriteKeyword(SyntaxKind.DotToken, writeSpace: false);
            else
                root = false;

            _outWriter.WriteSymbol(ns);
        }

        _outWriter.OpenBrace();

        return new Block(() =>
        {
            _outWriter.CloseBrace();
        });
    }

    public IDisposable WriteTypeDefinition(IEnumerable<SyntaxKind> accessibility, IEnumerable<SyntaxKind> keywords,
        string typeName, IEnumerable<string> baseTypeNames, IEnumerable<IEnumerable<SymbolDisplayPart>> constrains)
    {
        foreach (var keyword in accessibility)
        {
            WriteKeyword(keyword);
        }

        foreach (var keyword in keywords)
        {
            WriteKeyword(keyword);
        }

        _outWriter.WriteSymbol(typeName);
        _outWriter.WriteSpace();

        bool first = true;

        foreach (var baseSymbol in baseTypeNames)
        {
            if (first)
                WriteKeyword(SyntaxKind.ColonToken);
            else
                WriteKeyword(SyntaxKind.CommaToken);

            first = false;

            _outWriter.WriteSymbol(baseSymbol);
        }

        foreach (var currConstrain in constrains)
        {
            _outWriter.WriteSpace();
            foreach (var part in currConstrain)
            {
                _outWriter.WriteSymbol(part.ToString());
            }
        }

        _outWriter.OpenBrace();

        return new Block(() =>
        {
            _outWriter.CloseBrace();
        });
    }

    public void WriteAttribute(string attribute)
    {
        WriteKeyword(SyntaxKind.OpenBracketToken, writeSpace: false);
        _outWriter.WriteSymbol(attribute);
        WriteKeyword(SyntaxKind.CloseBracketToken, writeSpace: false);
        _outWriter.WriteLine();
    }

    public void WriteProperty(string definition, bool hasImplementation, bool hasGetMethod, bool hasSetMethod)
    {
        _outWriter.WriteSymbol(definition);

        if (hasGetMethod || hasSetMethod)
        {
            var _writeAccessorMethod = (SyntaxKind method) =>
            {
                WriteKeyword(method, writeSpace: false);
                if (hasImplementation)
                {
                    WriteImplementation();
                    _outWriter.WriteSpace();
                }
                else
                    WriteKeyword(SyntaxKind.SemicolonToken);
            };

            _outWriter.WriteSpace();
            WriteKeyword(SyntaxKind.OpenBraceToken);

            if (hasGetMethod)
            {
                _writeAccessorMethod(SyntaxKind.GetKeyword);
            }

            if (hasSetMethod)
            {
                _writeAccessorMethod(SyntaxKind.SetKeyword);
            }

            WriteKeyword(SyntaxKind.CloseBraceToken, writeSpace: false);
        }
        else
        {
            WriteKeyword(SyntaxKind.SemicolonToken, writeSpace: false);
        }

        _outWriter.WriteLine();
    }

    public void WriteEvent(string definition, bool hasAddMethod, bool hasRemoveMethod)
    {
        _outWriter.WriteSymbol(definition);

        if (hasAddMethod || hasRemoveMethod)
        {
            var _writeAccessorMethod = (SyntaxKind method) =>
            {
                WriteKeyword(method);
                WriteImplementation();
                _outWriter.WriteSpace();
            };

            _outWriter.WriteSpace();
            WriteKeyword(SyntaxKind.OpenBraceToken);

            if (hasAddMethod)
            {
                _writeAccessorMethod(SyntaxKind.AddKeyword);
            }

            if (hasRemoveMethod)
            {
                _writeAccessorMethod(SyntaxKind.RemoveKeyword);
            }

            WriteKeyword(SyntaxKind.CloseBraceToken, writeSpace: false);
        }
        else
        {
            WriteKeyword(SyntaxKind.SemicolonToken, writeSpace: false);
        }

        _outWriter.WriteLine();
    }

    public void Writemethod(string definition, bool hasImplementation)
    {
        _outWriter.WriteSymbol(definition);
        if (hasImplementation)
        {
            _outWriter.WriteSpace();
            WriteImplementation();
        }
        else
        {
            WriteKeyword(SyntaxKind.SemicolonToken);
        }

        _outWriter.WriteLine();
    }

    public void Dispose() => _outWriter.Dispose();

    /// ------------------------------------------------------

    private void WriteKeyword(SyntaxKind keyword, bool writeSpace = true)
    {
        _outWriter.WriteSymbol(SyntaxFacts.GetText(keyword));
        if (writeSpace)
            _outWriter.WriteSpace();
    }

    private void WriteImplementation()
    {
        WriteKeyword(SyntaxKind.OpenBraceToken);
        WriteKeyword(SyntaxKind.ThrowKeyword);
        WriteKeyword(SyntaxKind.NullKeyword, writeSpace: false);
        WriteKeyword(SyntaxKind.SemicolonToken);
        WriteKeyword(SyntaxKind.CloseBraceToken, writeSpace: false);
    }

    private class Block : IDisposable
    {
        private readonly Action _endBlock;

        public Block(Action endBlock) => _endBlock = endBlock;

        public void Dispose() => _endBlock();
    }
}
