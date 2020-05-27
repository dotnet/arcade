// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.DotNet.AsmDiff
{
    public sealed class DiffDocument
    {
        public AssemblySet Left { get; private set; }
        public AssemblySet Right { get; private set; }
        public ReadOnlyCollection<DiffLine> Lines { get; private set; }
        public ReadOnlyCollection<DiffApiDefinition> ApiDefinitions { get; private set; }

        public DiffDocument(AssemblySet left, AssemblySet right, IEnumerable<DiffLine> lines, IEnumerable<DiffApiDefinition> apiDefinitions)
        {
            Left = left;
            Right = right;
            Lines = new ReadOnlyCollection<DiffLine>(lines.ToArray());
            ApiDefinitions = new ReadOnlyCollection<DiffApiDefinition>(apiDefinitions.ToArray());
        }

        public bool IsDiff
        {
            get
            {
                var hasLeft = Left != null && !Left.IsEmpty;
                var hasRight = Right != null && !Right.IsEmpty;
                if (!hasLeft && !hasRight)
                    return false;

                return !hasLeft || hasRight;
            }
        }
    }

    public sealed class DiffLine
    {
        public DiffLineKind Kind { get; private set; }
        public ReadOnlyCollection<DiffToken> Tokens { get; private set; }

        public DiffLine(DiffLineKind kind, IEnumerable<DiffToken> tokens)
        {
            Kind = kind;
            Tokens = new ReadOnlyCollection<DiffToken>(tokens.ToArray());
        }

        public override string ToString()
        {
            return string.Concat(Tokens);
        }
    }

    public sealed class DiffToken
    {
        public DiffStyle Style { get; private set; }
        public DiffTokenKind Kind { get; set; }
        public string Text { get; private set; }

        public DiffToken(DiffStyle style, DiffTokenKind kind, string text)
        {
            Style = style;
            Text = text;
            Kind = kind;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    public enum DiffLineKind
    {
        Same,
        Added,
        Removed,
        Changed
    }

    [Flags]
    public enum DiffStyle
    {
        None = 0x00,
        Added = 0x01,
        Removed = 0x02,
        InterfaceMember = 0x04,
        InheritedMember = 0x08,
        NotCompatible = 0x10,
    }

    public enum DiffTokenKind
    {
        Text,
        Symbol,
        Identifier,
        Keyword,
        TypeName,
        LineBreak,
        Indent,
        Whitespace
    }
}
