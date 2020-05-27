// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Mappings;
using Microsoft.Cci.Writers;
using Microsoft.Cci.Writers.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.DotNet.AsmDiff
{
    internal sealed class ApiRecordingCSharpDiffWriter : DiffCSharpWriter, ICciDifferenceWriter
    {
        private DiffRecorder _diffRecorder;
        private MappingSettings _settings;
        private List<DiffApiDefinition> _apis = new List<DiffApiDefinition>();
        private Stack<List<DiffApiDefinition>> _apiStack = new Stack<List<DiffApiDefinition>>();
        private Stack<DiffApiDefinition> _apiDefinitionStack = new Stack<DiffApiDefinition>();

        public ApiRecordingCSharpDiffWriter(DiffRecorder diffRecorder, MappingSettings settings, bool includePseudoCustomAttributes)
            : base(diffRecorder, settings, Enumerable.Empty<DiffComment>(), includePseudoCustomAttributes)
        {
            _diffRecorder = diffRecorder;
            _settings = settings;
        }

        public List<DiffApiDefinition> ApiDefinitions
        {
            get { return _apis.ToList(); }
        }

        public new void Write(string oldAssembliesName, IEnumerable<IAssembly> oldAssemblies, string newAssembliesName, IEnumerable<IAssembly> newAssemblies)
        {
            AssemblySetMapping mapping;
            if (!string.IsNullOrEmpty(newAssembliesName))
            {
                _settings.ElementCount = 2;
                mapping = new AssemblySetMapping(_settings);
                mapping.AddMappings(oldAssemblies, newAssemblies);
            }
            else
            {
                _settings.ElementCount = 1;
                mapping = new AssemblySetMapping(_settings);
                mapping.AddMapping(0, oldAssemblies);
            }
            Visit(mapping);
        }

        private void PushApi<T>(ElementMapping<T> elementMapping)
            where T : class, IDefinition
        {
            var left = elementMapping[0];
            var right = elementMapping.ElementCount == 1
                            ? null
                            : elementMapping[1];

            var difference = elementMapping.Difference;

            var newChildren = new List<DiffApiDefinition>();
            var apiDefinition = new DiffApiDefinition(left, right, difference, newChildren)
            {
                StartLine = _diffRecorder.Line
            };
            _apis.Add(apiDefinition);

            _apiStack.Push(_apis);
            _apiDefinitionStack.Push(apiDefinition);
            _apis = newChildren;
        }

        private void PopApi()
        {
            var currentApi = _apiDefinitionStack.Pop();
            currentApi.EndLine = _diffRecorder.Line - 1;

            _apis = _apiStack.Pop();
        }

        public override void Visit(AssemblyMapping assembly)
        {
            _diffRecorder.CancellationToken.ThrowIfCancellationRequested();

            PushApi(assembly);
            base.Visit(assembly);
            PopApi();
        }

        public override void Visit(NamespaceMapping ns)
        {
            _diffRecorder.CancellationToken.ThrowIfCancellationRequested();

            PushApi(ns);
            base.Visit(ns);
            PopApi();
        }

        public override void Visit(TypeMapping type)
        {
            _diffRecorder.CancellationToken.ThrowIfCancellationRequested();

            PushApi(type);
            base.Visit(type);
            PopApi();
        }

        public override void Visit(MemberMapping member)
        {
            _diffRecorder.CancellationToken.ThrowIfCancellationRequested();

            var shouldVisit = !IsPropertyOrEventAccessor(member.Representative) &&
                              !IsEnumValueField(member.Representative) &&
                              !IsDelegateMember(member.Representative);

            if (shouldVisit)
                PushApi(member);

            base.Visit(member);

            if (shouldVisit)
                PopApi();
        }

        private static bool IsPropertyOrEventAccessor(ITypeDefinitionMember representative)
        {
            var methodDefinition = representative as IMethodDefinition;
            if (methodDefinition == null)
                return false;

            return methodDefinition.IsPropertyOrEventAccessor();
        }

        private static bool IsEnumValueField(ITypeDefinitionMember representative)
        {
            var isEnumMember = representative.ContainingTypeDefinition.IsEnum;
            if (!isEnumMember)
                return false;

            return representative.Name.Value == "value__";
        }

        private static bool IsDelegateMember(ITypeDefinitionMember representative)
        {
            return representative.ContainingTypeDefinition.IsDelegate;
        }
    }

    internal sealed class DiffRecorder : IStyleSyntaxWriter
    {
        private Stack<DiffStyle> _styleStack = new Stack<DiffStyle>();
        private bool _needIndent = true;
        private bool _skipNextWithspace;
        private bool _lastTokenWasLineBreak;

        public DiffRecorder(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }

        public List<DiffToken> Tokens { get; } = new List<DiffToken>();

        public int Line { get; private set; }

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
            CancellationToken.ThrowIfCancellationRequested();

            var tokenIsLineBreak = kind == DiffTokenKind.LineBreak;
            if (tokenIsLineBreak && _lastTokenWasLineBreak)
                return;

            if (tokenIsLineBreak)
                Line++;

            _lastTokenWasLineBreak = tokenIsLineBreak;

            var diffStyle = GetCurrentDiffStyle();
            var token = new DiffToken(diffStyle, kind, text);
            Tokens.Add(token);
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
    
    public static class DiffExtensions
    {
        public static bool HasStyle(this DiffToken token, DiffStyle diffStyle)
        {
            // Special case the zero-flag.
            if (diffStyle == DiffStyle.None)
                return token.Style == DiffStyle.None;

            return (token.Style & diffStyle) == diffStyle;
        }
    }

    internal sealed class DisposeAction : IDisposable
    {
        private Action _action;

        public DisposeAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            if (_action == null)
                return;

            _action();
            _action = null;
        }
    }
}
