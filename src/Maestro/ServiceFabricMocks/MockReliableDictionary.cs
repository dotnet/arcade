// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Data.Notifications;

namespace ServiceFabricMocks
{
    public class MockReliableDictionary<TKey, TValue> : IReliableDictionary2<TKey, TValue>
        where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        private readonly ConcurrentDictionary<TKey, TValue> dictionary = new ConcurrentDictionary<TKey, TValue>();

#pragma warning disable 0067
        public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>> DictionaryChanged;
#pragma warning restore 0067

        public Uri Name { get; set; }

        public Func<IReliableDictionary<TKey, TValue>, NotifyDictionaryRebuildEventArgs<TKey, TValue>, Task>
            RebuildNotificationAsyncCallback
        {
            set => throw new NotImplementedException();
        }

        public long Count => dictionary.Count();

        public Task AddAsync(ITransaction tx, TKey key, TValue value)
        {
            if (!dictionary.TryAdd(key, value))
            {
                throw new InvalidOperationException("key already exists: " + key);
            }

            return Task.FromResult(true);
        }

        public Task AddAsync(
            ITransaction tx,
            TKey key,
            TValue value,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (!dictionary.TryAdd(key, value))
            {
                throw new InvalidOperationException("key already exists: " + key);
            }

            return Task.FromResult(true);
        }

        public Task<TValue> AddOrUpdateAsync(
            ITransaction tx,
            TKey key,
            Func<TKey, TValue> addValueFactory,
            Func<TKey, TValue, TValue> updateValueFactory)
        {
            return Task.FromResult(dictionary.AddOrUpdate(key, addValueFactory, updateValueFactory));
        }

        public Task<TValue> AddOrUpdateAsync(
            ITransaction tx,
            TKey key,
            TValue addValue,
            Func<TKey, TValue, TValue> updateValueFactory)
        {
            return Task.FromResult(dictionary.AddOrUpdate(key, addValue, updateValueFactory));
        }

        public Task<TValue> AddOrUpdateAsync(
            ITransaction tx,
            TKey key,
            Func<TKey, TValue> addValueFactory,
            Func<TKey, TValue, TValue> updateValueFactory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(dictionary.AddOrUpdate(key, addValueFactory, updateValueFactory));
        }

        public Task<TValue> AddOrUpdateAsync(
            ITransaction tx,
            TKey key,
            TValue addValue,
            Func<TKey, TValue, TValue> updateValueFactory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(dictionary.AddOrUpdate(key, addValue, updateValueFactory));
        }

        public Task ClearAsync()
        {
            dictionary.Clear();

            return Task.FromResult(true);
        }

        public Task ClearAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            dictionary.Clear();

            return Task.FromResult(true);
        }

        public Task<bool> ContainsKeyAsync(ITransaction tx, TKey key)
        {
            return Task.FromResult(dictionary.ContainsKey(key));
        }

        public Task<bool> ContainsKeyAsync(ITransaction tx, TKey key, LockMode lockMode)
        {
            return Task.FromResult(dictionary.ContainsKey(key));
        }

        public Task<bool> ContainsKeyAsync(
            ITransaction tx,
            TKey key,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(dictionary.ContainsKey(key));
        }

        public Task<bool> ContainsKeyAsync(
            ITransaction tx,
            TKey key,
            LockMode lockMode,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(dictionary.ContainsKey(key));
        }

        public Task<ConditionalValue<TValue>> TryGetValueAsync(ITransaction tx, TKey key)
        {
            TValue value;
            bool result = dictionary.TryGetValue(key, out value);

            return Task.FromResult(new ConditionalValue<TValue>(result, value));
        }

        public Task<ConditionalValue<TValue>> TryGetValueAsync(ITransaction tx, TKey key, LockMode lockMode)
        {
            TValue value;
            bool result = dictionary.TryGetValue(key, out value);

            return Task.FromResult(new ConditionalValue<TValue>(result, value));
        }

        public Task<ConditionalValue<TValue>> TryGetValueAsync(
            ITransaction tx,
            TKey key,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            TValue value;
            bool result = dictionary.TryGetValue(key, out value);

            return Task.FromResult(new ConditionalValue<TValue>(result, value));
        }

        public Task<ConditionalValue<TValue>> TryGetValueAsync(
            ITransaction tx,
            TKey key,
            LockMode lockMode,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            TValue value;
            bool result = dictionary.TryGetValue(key, out value);

            return Task.FromResult(new ConditionalValue<TValue>(result, value));
        }

        public Task SetAsync(ITransaction tx, TKey key, TValue value)
        {
            dictionary[key] = value;

            return Task.FromResult(true);
        }

        public Task SetAsync(
            ITransaction tx,
            TKey key,
            TValue value,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            dictionary[key] = value;

            return Task.FromResult(true);
        }

        public Task<TValue> GetOrAddAsync(ITransaction tx, TKey key, Func<TKey, TValue> valueFactory)
        {
            return Task.FromResult(dictionary.GetOrAdd(key, valueFactory));
        }

        public Task<TValue> GetOrAddAsync(ITransaction tx, TKey key, TValue value)
        {
            return Task.FromResult(dictionary.GetOrAdd(key, value));
        }

        public Task<TValue> GetOrAddAsync(
            ITransaction tx,
            TKey key,
            Func<TKey, TValue> valueFactory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(dictionary.GetOrAdd(key, valueFactory));
        }

        public Task<TValue> GetOrAddAsync(
            ITransaction tx,
            TKey key,
            TValue value,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(dictionary.GetOrAdd(key, value));
        }

        public Task<bool> TryAddAsync(ITransaction tx, TKey key, TValue value)
        {
            return Task.FromResult(dictionary.TryAdd(key, value));
        }

        public Task<bool> TryAddAsync(
            ITransaction tx,
            TKey key,
            TValue value,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(dictionary.TryAdd(key, value));
        }

        public Task<ConditionalValue<TValue>> TryRemoveAsync(ITransaction tx, TKey key)
        {
            TValue outValue;
            return Task.FromResult(new ConditionalValue<TValue>(dictionary.TryRemove(key, out outValue), outValue));
        }

        public Task<ConditionalValue<TValue>> TryRemoveAsync(
            ITransaction tx,
            TKey key,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return TryRemoveAsync(tx, key);
        }

        public Task<bool> TryUpdateAsync(ITransaction tx, TKey key, TValue newValue, TValue comparisonValue)
        {
            return Task.FromResult(dictionary.TryUpdate(key, newValue, comparisonValue));
        }

        public Task<bool> TryUpdateAsync(
            ITransaction tx,
            TKey key,
            TValue newValue,
            TValue comparisonValue,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(dictionary.TryUpdate(key, newValue, comparisonValue));
        }

        public Task<long> GetCountAsync(ITransaction tx)
        {
            return Task.FromResult((long) dictionary.Count);
        }

        public Task<IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(ITransaction txn)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(ITransaction txn, EnumerationMode enumerationMode)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(
            ITransaction txn,
            EnumerationMode enumerationMode,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IAsyncEnumerable<KeyValuePair<TKey, TValue>>> IReliableDictionary<TKey, TValue>.CreateEnumerableAsync(
            ITransaction txn)
        {
            return Task.FromResult<IAsyncEnumerable<KeyValuePair<TKey, TValue>>>(
                new MockAsyncEnumerable<KeyValuePair<TKey, TValue>>(dictionary));
        }

        Task<IAsyncEnumerable<KeyValuePair<TKey, TValue>>> IReliableDictionary<TKey, TValue>.CreateEnumerableAsync(
            ITransaction txn,
            EnumerationMode enumerationMode)
        {
            return Task.FromResult<IAsyncEnumerable<KeyValuePair<TKey, TValue>>>(
                new MockAsyncEnumerable<KeyValuePair<TKey, TValue>>(
                    enumerationMode == EnumerationMode.Unordered
                        ? (IEnumerable<KeyValuePair<TKey, TValue>>) dictionary
                        : dictionary.OrderBy(x => x.Key)));
        }

        Task<IAsyncEnumerable<KeyValuePair<TKey, TValue>>> IReliableDictionary<TKey, TValue>.CreateEnumerableAsync(
            ITransaction txn,
            Func<TKey, bool> filter,
            EnumerationMode enumerationMode)
        {
            return Task.FromResult<IAsyncEnumerable<KeyValuePair<TKey, TValue>>>(
                new MockAsyncEnumerable<KeyValuePair<TKey, TValue>>(
                    enumerationMode == EnumerationMode.Unordered
                        ? dictionary.Where(x => filter(x.Key))
                        : dictionary.Where(x => filter(x.Key)).OrderBy(x => x.Key)));
        }

        public Task<long> GetCountAsync()
        {
            return Task.FromResult((long) dictionary.Count);
        }
    }
}
