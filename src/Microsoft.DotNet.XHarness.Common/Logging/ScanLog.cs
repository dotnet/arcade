// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.Common.Logging;

/// <summary>
/// Log that scans for a given tag and notifies when tag is found in the stream.
/// </summary>
public class ScanLog : Log
{
    private readonly string _tag;
    private readonly Action _tagFoundNotify;
    private readonly char[] _buffer;
    private int _startIndex;
    private bool _hasBeenFilled = false;

    public override bool Timestamp { get => false; set { } }

    public ScanLog(string tag, Action tagFoundNotify)
    {
        if (string.IsNullOrEmpty(tag))
        {
            throw new ArgumentException($"'{nameof(tag)}' cannot be null or empty.", nameof(tag));
        }

        _tag = tag;
        _tagFoundNotify = tagFoundNotify;
        _buffer = new char[_tag.Length];
        _startIndex = -1;
    }

    protected override void WriteImpl(string value)
    {
        foreach (var c in value)
        {
            Add(c);

            if (IsMatch())
            {
                _tagFoundNotify();
            }
        }
    }

    public override void Flush()
    {
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private void Add(char c)
    {
        _startIndex++;

        if (_startIndex == _buffer.Length - 1)
        {
            _hasBeenFilled = true;
        }

        _startIndex %= _buffer.Length;
        _buffer[_startIndex] = c;
    }

    private bool IsMatch()
    {
        if (!_hasBeenFilled)
        {
            return false;
        }

        for (int i = 1; i <= _buffer.Length; i++)
        {
            int index = (i + _startIndex) % _buffer.Length;
            if (_buffer[index] != _tag[i - 1])
            {
                return false;
            }
        }

        return true;
    }
}
