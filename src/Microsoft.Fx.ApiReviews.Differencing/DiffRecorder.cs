// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Cci.Writers.Syntax;
using Microsoft.Fx.ApiReviews.Differencing.Helpers;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    internal sealed class DiffRecorder : IStyleSyntaxWriter
    {
        private CancellationToken _cancellationToken;
        private List<DiffToken> _tokens = new List<DiffToken>();
        private Stack<DiffStyle> _styleStack = new Stack<DiffStyle>();
        private bool _needIndent = true;
        private bool _skipNextWithspace;
        private bool _lastTokenWasLineBreak;
        private int _line;

        public DiffRecorder(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken
        {
            get { return _cancellationToken; }
        }

        public List<DiffToken> Tokens
        {
            get { return _tokens; }
        }

        public int Line
        {
            get { return _line; }
        }

        public void Dispose()
        {
        }

        private void WriteIndentIfNeeded()
        {
            if (!_needIndent)
                return;

            WriteToken(DiffTokenKind.Indent, new string(' ', IndentLevel * 4));
            _needIndent = false;
        }

        private void WriteToken(DiffTokenKind kind, string text)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            var tokenIsLineBreak = kind == DiffTokenKind.LineBreak;
            if (tokenIsLineBreak && _lastTokenWasLineBreak)
                return;

            if (tokenIsLineBreak)
                _line++;

            _lastTokenWasLineBreak = tokenIsLineBreak;

            var diffStyle = GetCurrentDiffStyle();
            var token = new DiffToken(diffStyle, kind, text);
            _tokens.Add(token);
        }

        private DiffStyle GetCurrentDiffStyle()
        {
            return _styleStack.Aggregate(DiffStyle.None, (current, diffStyle) => current | diffStyle);
        }

        public void Write(string str)
        {
            // HACK: Work around issue where member attributes are emitted
            //       using a textual linebreak
            if (str == "\r\n" || str == "\r" || str == "\n")
            {
                // HACK: We should ignore the next whitespace text to avoid double indenting.
                //       However, setting _needIndent to false is not a good solution either
                //       as it assumes that the writer uses the same level of indentation as
                //       we are (which is not the case).
                WriteLine();
                _skipNextWithspace = true;
                return;
            }
            
            // If the text is whitespace only, recored it as such.
            if (str.Trim().Length == 0)
            {
                WriteWhitespace(str);
                return;
            }

            // HACK: Work around issue where member attributes are emitted
            //       using a textual indent
            if (_lastTokenWasLineBreak && str.Trim().Length == 0)
            {
                return;
            }

            WriteIndentIfNeeded();
            WriteToken(DiffTokenKind.Text, str);
        }

        private void WriteWhitespace(string str)
        {
            if (_skipNextWithspace)
            {
                _skipNextWithspace = false;
                return;
            }

            WriteIndentIfNeeded();
            WriteToken(DiffTokenKind.Whitespace, str);
        }

        public void WriteSymbol(string symbol)
        {
            WriteIndentIfNeeded();
            WriteToken(DiffTokenKind.Symbol, symbol);
        }

        public void WriteIdentifier(string id)
        {
            WriteIndentIfNeeded();
            WriteToken(DiffTokenKind.Identifier, id);
        }

        public void WriteKeyword(string keyword)
        {
            WriteIndentIfNeeded();
            WriteToken(DiffTokenKind.Keyword, keyword);
        }

        public void WriteTypeName(string typeName)
        {
            WriteIndentIfNeeded();
            WriteToken(DiffTokenKind.TypeName, typeName);
        }

        public void WriteLine()
        {
            WriteToken(DiffTokenKind.LineBreak, Environment.NewLine);
            _needIndent = true;
        }

        public int IndentLevel { get; set; }

        public IDisposable StartStyle(SyntaxStyle style, object context)
        {
            var convertedStyle = ConvertStyle(style);
            _styleStack.Push(convertedStyle);
            return new DisposeAction(() => _styleStack.Pop());
        }

        private static DiffStyle ConvertStyle(SyntaxStyle style)
        {
            switch (style)
            {
                case SyntaxStyle.Added:
                    return DiffStyle.Added;
                case SyntaxStyle.Removed:
                    return DiffStyle.Removed;
                case SyntaxStyle.InterfaceMember:
                    return DiffStyle.InterfaceMember;
                case SyntaxStyle.InheritedMember:
                    return DiffStyle.InheritedMember;
                case SyntaxStyle.NotCompatible:
                    return DiffStyle.NotCompatible;
                default:
                    throw new ArgumentOutOfRangeException("style");
            }
        }
    }
}
