namespace ServiceFabricMocks
{
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    public class MockReliableQueue<T> : IReliableQueue<T>
    {
        private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

        public Task EnqueueAsync(ITransaction tx, T item, TimeSpan timeout, CancellationToken cancellationToken)
        {
            this.queue.Enqueue(item);

            return Task.FromResult(true);
        }

        public Task EnqueueAsync(ITransaction tx, T item)
        {
            this.queue.Enqueue(item);

            return Task.FromResult(true);
        }

        public Task<ConditionalValue<T>> TryDequeueAsync(ITransaction tx, TimeSpan timeout, CancellationToken cancellationToken)
        {
            T item;
            bool result = this.queue.TryDequeue(out item);

            return Task.FromResult((ConditionalValue<T>)Activator.CreateInstance(typeof(ConditionalValue<T>), result, item));
        }

        public Task<ConditionalValue<T>> TryDequeueAsync(ITransaction tx)
        {
            T item;
            bool result = this.queue.TryDequeue(out item);

            return Task.FromResult((ConditionalValue<T>)Activator.CreateInstance(typeof(ConditionalValue<T>), result, item));
        }

        public Task<ConditionalValue<T>> TryPeekAsync(ITransaction tx, LockMode lockMode, TimeSpan timeout, CancellationToken cancellationToken)
        {
            T item;
            bool result = this.queue.TryPeek(out item);

            return Task.FromResult((ConditionalValue<T>)Activator.CreateInstance(typeof(ConditionalValue<T>), result, item));
        }

        public Task<ConditionalValue<T>> TryPeekAsync(ITransaction tx, LockMode lockMode)
        {
            T item;
            bool result = this.queue.TryPeek(out item);

            return Task.FromResult((ConditionalValue<T>)Activator.CreateInstance(typeof(ConditionalValue<T>), result, item));
        }

        public Task<ConditionalValue<T>> TryPeekAsync(ITransaction tx, TimeSpan timeout, CancellationToken cancellationToken)
        {
            T item;
            bool result = this.queue.TryPeek(out item);

            return Task.FromResult((ConditionalValue<T>)Activator.CreateInstance(typeof(ConditionalValue<T>), result, item));
        }

        public Task<ConditionalValue<T>> TryPeekAsync(ITransaction tx)
        {
            T item;
            bool result = this.queue.TryPeek(out item);

            return Task.FromResult((ConditionalValue<T>)Activator.CreateInstance(typeof(ConditionalValue<T>), result, item));
        }

        public Task ClearAsync()
        {
            while (!this.queue.IsEmpty)
            {
                T result;
                this.queue.TryDequeue(out result);
            }

            return Task.FromResult(true);
        }

        public Task<long> GetCountAsync()
        {
            return Task.FromResult((long)this.queue.Count);
        }

        public Task<IAsyncEnumerable<T>> CreateEnumerableAsync(ITransaction tx)
        {
            return Task.FromResult<IAsyncEnumerable<T>>(new MockAsyncEnumerable<T>(this.queue));
        }

        public Task<long> GetCountAsync(ITransaction tx)
        {
            return Task.FromResult<long>(this.queue.Count);
        }

        public Uri Name { get; set; }

    }
}
