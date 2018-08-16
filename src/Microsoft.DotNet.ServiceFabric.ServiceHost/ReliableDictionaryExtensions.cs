// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace Microsoft.Helix.ServiceHost
{
    public static class ReliableDictionaryExtensions
    {
        public static async Task<TValue> GetValueOrDefaultAsync<TKey, TValue>(
            this IReliableDictionary<TKey, TValue> dict,
            ITransaction transaction,
            TKey key) where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            ConditionalValue<TValue> maybeValue = await dict.TryGetValueAsync(transaction, key);
            if (maybeValue.HasValue)
            {
                return maybeValue.Value;
            }

            return default;
        }

        public static async Task ForEachAsync<TKey, TValue>(
            [NotNull] this IReliableDictionary<TKey, TValue> dict,
            [NotNull] ITransaction transaction,
            CancellationToken token,
            [NotNull] [InstantHandle] Action<TKey, TValue> block) where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            await (await dict.CreateEnumerableAsync(transaction, EnumerationMode.Unordered)).ForEach(token, block);
        }

        public static async Task ForEachAsync<TKey, TValue>(
            [NotNull] this IReliableDictionary<TKey, TValue> dict,
            [NotNull] ITransaction transaction,
            CancellationToken token,
            [NotNull] [InstantHandle] Func<TKey, TValue, Task> block) where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            await (await dict.CreateEnumerableAsync(transaction, EnumerationMode.Unordered)).ForEach(token, block);
        }

        public static async Task ForEachKeyAsync<TKey, TValue>(
            [NotNull] this IReliableDictionary2<TKey, TValue> dict,
            [NotNull] ITransaction transaction,
            CancellationToken token,
            [NotNull] [InstantHandle] Action<TKey> block) where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            await (await dict.CreateKeyEnumerableAsync(transaction, EnumerationMode.Unordered)).ForEach(token, block);
        }

        public static async Task<IList<KeyValuePair<TKey, TValue>>> ToListAsync<TKey, TValue>(
            [NotNull] this IReliableDictionary<TKey, TValue> dict,
            [NotNull] ITransaction tx) where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            var result = new List<KeyValuePair<TKey, TValue>>((int) await dict.GetCountAsync(tx));

            IAsyncEnumerable<KeyValuePair<TKey, TValue>> enumerable = await dict.CreateEnumerableAsync(tx);
            using (IAsyncEnumerator<KeyValuePair<TKey, TValue>> enumerator = enumerable.GetAsyncEnumerator())
            {
                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    result.Add(enumerator.Current);
                }
            }

            return result;
        }
    }
}
