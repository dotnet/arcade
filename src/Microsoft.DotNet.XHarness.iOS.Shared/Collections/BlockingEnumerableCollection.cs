// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Collections;

// This is a collection whose enumerator will wait enumerating until 
// the collection has been marked as completed (but the enumerator can still
// be created; this allows the creation of linq queries whose execution is
// delayed until later).
public class BlockingEnumerableCollection<T> : IEnumerable<T> where T : class
{
    private readonly List<T> _list = new();
    private TaskCompletionSource<bool> _completed = new();

    public int Count
    {
        get
        {
            WaitForCompletion();
            return _list.Count;
        }
    }

    public void Add(T device)
    {
        _list.Add(device);
    }

    public void SetCompleted() => _completed.TrySetResult(true);

    private void WaitForCompletion() => _completed.Task.Wait();

    public void Reset()
    {
        _completed = new TaskCompletionSource<bool>();
        _list.Clear();
    }

    public IEnumerator<T> GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private class Enumerator : IEnumerator<T>
    {
        private readonly BlockingEnumerableCollection<T> _collection;
        private IEnumerator<T>? _enumerator;

        public Enumerator(BlockingEnumerableCollection<T> collection)
        {
            _collection = collection;
        }

        public T Current => _enumerator?.Current ?? throw new InvalidOperationException("Please call MoveNext() first!");

        object IEnumerator.Current => _enumerator?.Current ?? throw new InvalidOperationException("Please call MoveNext() first!");

        public void Dispose() => _enumerator?.Dispose();

        public bool MoveNext()
        {
            _collection.WaitForCompletion();
            if (_enumerator == null)
            {
                _enumerator = _collection._list.GetEnumerator();
            }

            return _enumerator.MoveNext();
        }

        public void Reset() => _enumerator?.Reset();
    }
}
