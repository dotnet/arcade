// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Util
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<T> NullAsEmpty<T>(this IEnumerable<T> source)
        {
            return source ?? Enumerable.Empty<T>();
        }

        public static TValue GetOrDefault<TKey, TValue>(
            this IDictionary<TKey, TValue> attributes,
            TKey key)
        {
            TValue value;
            attributes.TryGetValue(key, out value);
            return value;
        }
    }
}
