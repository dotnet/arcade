// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Microsoft.Helix.ServiceHost
{
    public static class AsyncEnumerableExtensions
    {
        public static async Task ForEach<TKey, TValue>(
            [NotNull] this ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>> enumerable,
            CancellationToken token,
            [NotNull, InstantHandle] Action<TKey, TValue> block)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException(nameof(enumerable));
            }
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            using (ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<TKey, TValue>> enumerator = enumerable.GetAsyncEnumerator())
            {
                while (await enumerator.MoveNextAsync(token))
                {
                    KeyValuePair<TKey, TValue> pair = enumerator.Current;
                    block(pair.Key, pair.Value);
                }
            }
        }

        public static async Task ForEach<TKey, TValue>(
            [NotNull] this ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>> enumerable,
            CancellationToken token,
            [NotNull, InstantHandle] Func<TKey, TValue, Task> block)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException(nameof(enumerable));
            }
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            using (ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<TKey, TValue>> enumerator = enumerable.GetAsyncEnumerator())
            {
                while (await enumerator.MoveNextAsync(token))
                {
                    KeyValuePair<TKey, TValue> pair = enumerator.Current;
                    await block(pair.Key, pair.Value);
                }
            }
        }

        public static async Task ForEach<T>(
            [NotNull] this ServiceFabric.Data.IAsyncEnumerable<T> enumerable,
            CancellationToken token,
            [NotNull, InstantHandle] Action<T> block)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException(nameof(enumerable));
            }
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            using (ServiceFabric.Data.IAsyncEnumerator<T> enumerator = enumerable.GetAsyncEnumerator())
            {
                while (await enumerator.MoveNextAsync(token))
                {
                    block(enumerator.Current);
                }
            }
        }
    }
}
