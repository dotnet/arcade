// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XUnitRunnerUap
{
    internal static class DictionaryExtensions
    {
        public static void Add<TKey, TValue>(this IDictionary<TKey, List<TValue>> dictionary, TKey key, TValue value)
        {
            dictionary.GetOrAdd(key).Add(value);
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            where TValue : new()
        {
            return dictionary.GetOrAdd(key, () => new TValue());
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> newValue)
        {
            if (!dictionary.TryGetValue(key, out TValue result))
            {
                result = newValue();
                dictionary[key] = result;
            }

            return result;
        }
    }
}
