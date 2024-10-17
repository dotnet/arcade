// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.XHarness.Common.Utilities;

public class StringUtils
{
    private static readonly char s_shellQuoteChar;
    private static readonly char[] s_mustQuoteCharacters = { ' ', '\'', ',', '$', '\\' };
    private static readonly char[] s_mustQuoteCharactersProcess = { ' ', '\\', '"', '\'' };

    static StringUtils()
    {
        PlatformID pid = Environment.OSVersion.Platform;
        if ((int)pid != 128 && pid != PlatformID.Unix && pid != PlatformID.MacOSX)
        {
            s_shellQuoteChar = '"'; // Windows
        }
        else
        {
            s_shellQuoteChar = '\''; // !Windows
        }
    }

    public static string FormatArguments(params string[] arguments) => FormatArguments((IList<string>)arguments);

    public static string FormatArguments(IList<string> arguments) => string.Join(" ", QuoteForProcess(arguments) ?? Array.Empty<string>());

    private static string[]? QuoteForProcess(params string[] array)
    {
        if (array == null || array.Length == 0)
        {
            return array;
        }

        var rv = new string[array.Length];
        for (var i = 0; i < array.Length; i++)
        {
            rv[i] = QuoteForProcess(array[i]);
        }

        return rv;
    }

    public static string Quote(string? f)
    {
        if (string.IsNullOrEmpty(f))
        {
            return f ?? string.Empty;
        }

        if (f.IndexOfAny(s_mustQuoteCharacters) == -1)
        {
            return f;
        }

        var s = new StringBuilder();

        s.Append(s_shellQuoteChar);
        foreach (var c in f)
        {
            if (c == '\'' || c == '"' || c == '\\')
            {
                s.Append('\\');
            }

            s.Append(c);
        }
        s.Append(s_shellQuoteChar);

        return s.ToString();
    }

    // Quote input according to how System.Diagnostics.Process needs it quoted.
    private static string QuoteForProcess(string f)
    {
        if (string.IsNullOrEmpty(f))
        {
            return f ?? string.Empty;
        }

        if (f.IndexOfAny(s_mustQuoteCharactersProcess) == -1)
        {
            return f;
        }

        var s = new StringBuilder();

        s.Append('"');
        foreach (var c in f)
        {
            if (c == '"')
            {
                s.Append('\\');
                s.Append(c).Append(c);
            }
            else if (c == '\\')
            {
                s.Append(c);
            }
            s.Append(c);
        }
        s.Append('"');

        return s.ToString();
    }

    private static string[]? QuoteForProcess(IList<string> arguments)
    {
        if (arguments == null)
        {
            return Array.Empty<string>();
        }

        return QuoteForProcess(arguments.ToArray());
    }
}
