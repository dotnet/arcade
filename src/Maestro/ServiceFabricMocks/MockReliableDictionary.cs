namespace ServiceFabricMocks
{
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using Microsoft.ServiceFabric.Data.Notifications;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class MockReliableDictionary<TKey, TValue> : IReliableDictionary2<TKey, TValue>
        where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        private ConcurrentDictionary<TKey, TValue> dictionary = new ConcurrentDictionary<TKey, TValue>();

#pragma warning disable 0067
        public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>> DictionaryChanged;
#pragma warning restore 0067

        public Uri Name { get; set; }

        public Func<IReliableDictionary<TKey, TValue>, NotifyDictionaryRebuildEventArgs<TKey, TValue>, Task> RebuildNotificationAsyncCallback
        {
            set
            {
                throw new NotImplementedException();
            }
        }

        public long Count
        {
            get
            {
                return this.dictionary.Count();
            }
        }

        public Task AddAsync(ITransaction tx, TKey key, TValue value)
        {
            if (!this.dictionary.TryAdd(key, value))
            {
                throw new InvalidOperationException("key already exists: " + key.ToString());
            }


            return Task.FromResult(true);
        }

        public Task AddAsync(ITransaction tx, TKey key, TValue value, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!this.dictionary.TryAdd(key, value))
            {
                throw new InvalidOperationException("key already exists: " + key.ToString());
            }

            return Task.FromResult(true);
        }

        public Task<TValue> AddOrUpdateAsync(ITransaction tx, TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory)
        {
            return Task.FromResult(this.dictionary.AddOrUpdate(key, addValueFactory, updateValueFactory));
        }

        public Task<TValue> AddOrUpdateAsync(ITransaction tx, TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
        {
            return Task.FromResult(this.dictionary.AddOrUpdate(key, addValue, updateValueFactory));
        }

        public Task<TValue> AddOrUpdateAsync(
            ITransaction tx, TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(this.dictionary.AddOrUpdate(key, addValueFactory, updateValueFactory));
        }

        public Task<TValue> AddOrUpdateAsync(
            ITransaction tx, TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.dictionary.AddOrUpdate(key, addValue, updateValueFactory));
        }

        public Task ClearAsync()
        {
            this.dictionary.Clear();

            return Task.FromResult(true);
        }

        public Task ClearAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            this.dictionary.Clear();

            return Task.FromResult(true);
        }

        public Task<bool> ContainsKeyAsync(ITransaction tx, TKey key)
        {
            return Task.FromResult(this.dictionary.ContainsKey(key));
        }

        public Task<bool> ContainsKeyAsync(ITransaction tx, TKey key, LockMode lockMode)
        {
            return Task.FromResult(this.dictionary.ContainsKey(key));
        }

        public Task<bool> ContainsKeyAsync(ITransaction tx, TKey key, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.dictionary.ContainsKey(key));
        }

        public Task<bool> ContainsKeyAsync(ITransaction tx, TKey key, LockMode lockMode, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.dictionary.ContainsKey(key));
        }

        public Task<ConditionalValue<TValue>> TryGetValueAsync(ITransaction tx, TKey key)
        {
            TValue value;
            bool result = this.dictionary.TryGetValue(key, out value);

            return Task.FromResult(new ConditionalValue<TValue>(result, value));
        }

        public Task<ConditionalValue<TValue>> TryGetValueAsync(ITransaction tx, TKey key, LockMode lockMode)
        {
            TValue value;
            bool result = this.dictionary.TryGetValue(key, out value);

            return Task.FromResult(new ConditionalValue<TValue>(result, value));
        }

        public Task<ConditionalValue<TValue>> TryGetValueAsync(ITransaction tx, TKey key, TimeSpan timeout, CancellationToken cancellationToken)
        {
            TValue value;
            bool result = this.dictionary.TryGetValue(key, out value);

            return Task.FromResult(new ConditionalValue<TValue>(result, value));
        }

        public Task<ConditionalValue<TValue>> TryGetValueAsync(
            ITransaction tx, TKey key, LockMode lockMode, TimeSpan timeout, CancellationToken cancellationToken)
        {
            TValue value;
            bool result = this.dictionary.TryGetValue(key, out value);

            return Task.FromResult(new ConditionalValue<TValue>(result, value));
        }

        public Task SetAsync(ITransaction tx, TKey key, TValue value)
        {
            this.dictionary[key] = value;

            return Task.FromResult(true);
        }

        public Task SetAsync(ITransaction tx, TKey key, TValue value, TimeSpan timeout, CancellationToken cancellationToken)
        {
            this.dictionary[key] = value;

            return Task.FromResult(true);
        }

        public Task<TValue> GetOrAddAsync(ITransaction tx, TKey key, Func<TKey, TValue> valueFactory)
        {
            return Task.FromResult(this.dictionary.GetOrAdd(key, valueFactory));
        }

        public Task<TValue> GetOrAddAsync(ITransaction tx, TKey key, TValue value)
        {
            return Task.FromResult(this.dictionary.GetOrAdd(key, value));
        }

        public Task<TValue> GetOrAddAsync(ITransaction tx, TKey key, Func<TKey, TValue> valueFactory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.dictionary.GetOrAdd(key, valueFactory));
        }

        public Task<TValue> GetOrAddAsync(ITransaction tx, TKey key, TValue value, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.dictionary.GetOrAdd(key, value));
        }

        public Task<bool> TryAddAsync(ITransaction tx, TKey key, TValue value)
        {
            return Task.FromResult(this.dictionary.TryAdd(key, value));
        }

        public Task<bool> TryAddAsync(ITransaction tx, TKey key, TValue value, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.dictionary.TryAdd(key, value));
        }

        public Task<ConditionalValue<TValue>> TryRemoveAsync(ITransaction tx, TKey key)
        {
            TValue outValue;
            return Task.FromResult(new ConditionalValue<TValue>(this.dictionary.TryRemove(key, out outValue), outValue));
        }

        public Task<ConditionalValue<TValue>> TryRemoveAsync(ITransaction tx, TKey key, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return this.TryRemoveAsync(tx, key);
        }

        public Task<bool> TryUpdateAsync(ITransaction tx, TKey key, TValue newValue, TValue comparisonValue)
        {
            return Task.FromResult(this.dictionary.TryUpdate(key, newValue, comparisonValue));
        }

        public Task<bool> TryUpdateAsync(
            ITransaction tx, TKey key, TValue newValue, TValue comparisonValue, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.dictionary.TryUpdate(key, newValue, comparisonValue));
        }

        public Task<long> GetCountAsync()
        {
            return Task.FromResult((long)this.dictionary.Count);
        }

        public Task<long> GetCountAsync(ITransaction tx)
        {
            return Task.FromResult((long)this.dictionary.Count);
        }

        public Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(ITransaction txn)
        {
            throw new NotImplementedException();
        }

        public Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(ITransaction txn, EnumerationMode enumerationMode)
        {
            throw new NotImplementedException();
        }

        public Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(ITransaction txn, EnumerationMode enumerationMode, TimeSpan timeout, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>> IReliableDictionary<TKey, TValue>.CreateEnumerableAsync(ITransaction txn)
        {
            return Task.FromResult<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>>(new MockAsyncEnumerable<KeyValuePair<TKey, TValue>>(this.dictionary));
        }

        Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>> IReliableDictionary<TKey, TValue>.CreateEnumerableAsync(ITransaction txn, EnumerationMode enumerationMode)
        {
            return Task.FromResult<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>>(new MockAsyncEnumerable<KeyValuePair<TKey, TValue>>(
                        enumerationMode == EnumerationMode.Unordered
                        ? (IEnumerable<KeyValuePair<TKey, TValue>>)this.dictionary
                        : this.dictionary.OrderBy(x => x.Key)));
        }

        Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>> IReliableDictionary<TKey, TValue>.CreateEnumerableAsync(ITransaction txn, Func<TKey, bool> filter, EnumerationMode enumerationMode)
        {
            return Task.FromResult<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>>(new MockAsyncEnumerable<KeyValuePair<TKey, TValue>>(
    enumerationMode == EnumerationMode.Unordered
        ? this.dictionary.Where(x => filter(x.Key))
        : this.dictionary.Where(x => filter(x.Key)).OrderBy(x => x.Key)));
        }
    }
}
