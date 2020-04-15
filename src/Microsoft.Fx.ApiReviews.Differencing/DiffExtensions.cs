// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public static class DiffExtensions
    {
        public static bool HasStyle(this DiffToken token, DiffStyle diffStyle)
        {
            // Special case the zero-flag.
            if (diffStyle == DiffStyle.None)
                return token.Style == DiffStyle.None;

            return (token.Style & diffStyle) == diffStyle;
        }

        public static string GetText(this DiffSide diffSide)
        {
            var lines = diffSide.Document.Lines.Select(line => GetText(line, diffSide.Version));
            return string.Join(Environment.NewLine, lines);
        }

        public static string GetText(this DiffLine line, DiffVersion version)
        {
            return line.IsLineVirtual(version)
                       ? string.Empty
                       : string.Concat(line.Tokens.Where(t => t.IsTokenVisible(version)).Select(t => t.Text));
        }

        public static bool IsLineVirtual(this DiffLine line, DiffVersion version)
        {
            return version == DiffVersion.Left && line.Kind == DiffLineKind.Added ||
                   version == DiffVersion.Right && line.Kind == DiffLineKind.Removed;
        }

        public static bool IsTokenVisible(this DiffToken token, DiffVersion version)
        {
            var ignoredStyle = version == DiffVersion.Left
                                   ? DiffStyle.Added
                                   : DiffStyle.Removed;

            var isIgnored = version != DiffVersion.Combined && token.HasStyle(ignoredStyle);

            // HACK: Sometimes, indents are considered part of the change, sometimes
            //       they are not. The right thing for indentation is to treat
            //       it as "Same".
            return !isIgnored || token.Kind == DiffTokenKind.Indent;
        }
    }
}
