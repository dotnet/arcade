// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Writes C# source code into IO.File or Console.
/// </summary>
public class CSharpSyntaxWriter: ISyntaxWriter
{
    private readonly StreamWriter _streamWriter;

    public CSharpSyntaxWriter(StreamWriter streamWriter) => _streamWriter = streamWriter;

    public IDisposable WriteNamespace(IEnumerable<string> namespacePath)
    {
        WriteKeyword(SyntaxKind.NamespaceKeyword);

        bool root = true;
        foreach (var ns in namespacePath)
        {
            if (!root)
            {
                WriteKeyword(SyntaxKind.DotToken, writeSpace: false);
            }
            else
            {
                root = false;
            }

            _streamWriter.Write(ns);
        }

        OpenBrace();

        return new Block(() =>
        {
            CloseBrace();
        });
    }

    public IDisposable WriteTypeDefinition(IEnumerable<SyntaxKind> accessibility, IEnumerable<SyntaxKind> keywords,
        string typeName, IEnumerable<string> baseTypeNames, IEnumerable<IEnumerable<SymbolDisplayPart>> constraints)
    {
        foreach (var keyword in accessibility)
        {
            WriteKeyword(keyword);
        }

        foreach (var keyword in keywords)
        {
            WriteKeyword(keyword);
        }

        _streamWriter.Write(typeName);
        WriteSpace();

        bool first = true;

        foreach (var baseSymbol in baseTypeNames)
        {
            if (first)
            {
                WriteKeyword(SyntaxKind.ColonToken);
            }
            else
            {
                WriteKeyword(SyntaxKind.CommaToken);
            }

            first = false;

            _streamWriter.Write(baseSymbol);
        }

        foreach (var currConstrain in constraints)
        {
            WriteSpace();
            foreach (var part in currConstrain)
            {
                _streamWriter.Write(part.ToString());
            }
        }

        OpenBrace();

        return new Block(() =>
        {
            CloseBrace();
        });
    }

    public void WriteAttribute(string attribute)
    {
        WriteKeyword(SyntaxKind.OpenBracketToken, writeSpace: false);
        _streamWriter.Write(attribute);
        WriteKeyword(SyntaxKind.CloseBracketToken, writeSpace: false);
        _streamWriter.WriteLine();
    }

    public void WriteProperty(string definition, bool hasImplementation, bool hasGetMethod, bool hasSetMethod)
    {
        _streamWriter.Write(definition);

        if (hasGetMethod || hasSetMethod)
        {
            var _writeAccessorMethod = (SyntaxKind method) =>
            {
                WriteKeyword(method, writeSpace: false);
                if (hasImplementation)
                {
                    WriteImplementation();
                    WriteSpace();
                }
                else
                    WriteKeyword(SyntaxKind.SemicolonToken);
            };

            WriteSpace();
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

        _streamWriter.WriteLine();
    }

    public void WriteEvent(string definition, bool hasAddMethod, bool hasRemoveMethod)
    {
        _streamWriter.Write(definition);

        if (hasAddMethod || hasRemoveMethod)
        {
            var _writeAccessorMethod = (SyntaxKind method) =>
            {
                WriteKeyword(method);
                WriteImplementation();
                WriteSpace();
            };

            WriteSpace();
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

        _streamWriter.WriteLine();
    }

    public void WriteMethod(string definition, bool hasImplementation)
    {
        _streamWriter.Write(definition);
        if (hasImplementation)
        {
            WriteSpace();
            WriteImplementation();
        }
        else
        {
            WriteKeyword(SyntaxKind.SemicolonToken);
        }

        _streamWriter.WriteLine();
    }

    public void Dispose() => _streamWriter.Dispose();

    #region Private methods

    private void WriteSpace()
    {
        _streamWriter.Write(' ');
    }

    private void OpenBrace()
    {
        WriteSpace();
        WriteKeyword(SyntaxKind.OpenBraceToken, writeSpace: false);
        _streamWriter.WriteLine();
    }

    private void CloseBrace()
    {
        WriteKeyword(SyntaxKind.CloseBraceToken, writeSpace: false);
        _streamWriter.WriteLine();
    }

    private void WriteKeyword(SyntaxKind keyword, bool writeSpace = true)
    {
        _streamWriter.Write(SyntaxFacts.GetText(keyword));
        if (writeSpace)
        {
            WriteSpace();
        }
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

    #endregion
}
