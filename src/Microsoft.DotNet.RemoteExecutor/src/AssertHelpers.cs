// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.RemoteExecutor;

internal static class Assert
{
    public static void True(
        bool? condition,
        string? userMessage = null)
    {
        if (condition != true)
        {
            ThrowActualExpectedException("True", condition?.ToString() ?? "(null)", userMessage ?? "Assert.True() Failure");
        }
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            ThrowActualExpectedException(expected, actual, null);
        }
    }

    public static void All<T>(IEnumerable<T> collection, Action<T> assert)
    {
        foreach (var item in collection)
        {
            assert(item);
        }
    }

    private static void ThrowActualExpectedException(
        object? expected,
        object? actual,
        string? userMessage)
    {
        var message = $"{userMessage}{Environment.NewLine}Expected: {expected?.ToString() ?? "(null)"}{Environment.NewLine}Actual: {actual?.ToString() ?? "(null)"}";
        throw new Exception(message);
    }
}