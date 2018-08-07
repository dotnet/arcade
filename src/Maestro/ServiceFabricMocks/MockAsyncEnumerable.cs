namespace ServiceFabricMocks
{
    using Microsoft.ServiceFabric.Data;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Simple wrapper for a synchronous IEnumerable of T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class MockAsyncEnumerable<T> : Microsoft.ServiceFabric.Data.IAsyncEnumerable<T>
    {
        private IEnumerable<T> enumerable;

        public MockAsyncEnumerable(IEnumerable<T> enumerable)
        {
            this.enumerable = enumerable;
        }

        public Microsoft.ServiceFabric.Data.IAsyncEnumerator<T> GetAsyncEnumerator()
        {
            return new MockAsyncEnumerator<T>(this.enumerable.GetEnumerator());
        }
    }

    /// <summary>
    /// Simply wrapper for a synchronous IEnumerator of T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class MockAsyncEnumerator<T> : Microsoft.ServiceFabric.Data.IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> enumerator;

        public MockAsyncEnumerator(IEnumerator<T> enumerator)
        {
            this.enumerator = enumerator;
        }


        public T Current
        {
            get
            {
                return this.enumerator.Current;
            }
        }

        public void Dispose()
        {
            this.enumerator.Dispose();
        }

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(this.enumerator.MoveNext());
        }

        public void Reset()
        {
            this.enumerator.Reset();
        }
    }

}
