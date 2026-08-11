// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>
    /// A bounded, object-oriented asynchronous work queue. Producers observe backpressure and
    /// worker failures are surfaced when the queue is drained; they are never fire-and-forget.
    /// </summary>
    internal sealed class AsyncWorkQueue<T> : IDisposable
    {
        private readonly Channel<T> _channel;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task[] _workers;
        private readonly Func<T, CancellationToken, Task> _handler;
        private readonly object _sync = new();
        private Exception _failure;
        private bool _completed;

        public AsyncWorkQueue(int parallelism, int capacity, Func<T, CancellationToken, Task> handler)
        {
            if (parallelism <= 0) throw new ArgumentOutOfRangeException(nameof(parallelism));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            _handler = handler ?? throw new ArgumentNullException(nameof(handler));

            // Waiting on a full channel is the backpressure mechanism. It bounds both queued
            // objects and the amount of producer work that can get ahead of the consumers.
            _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false,
            });

            _workers = new Task[parallelism];
            for (int i = 0; i < _workers.Length; i++)
            {
                // Workers start immediately and live for the queue's lifetime; no per-item
                // Task.Run is needed, so scheduling and failure ownership remain explicit.
                _workers[i] = ConsumeAsync();
            }
        }

        public async ValueTask EnqueueAsync(T item, CancellationToken cancellationToken)
        {
            ThrowIfFailed();
            await _channel.Writer.WriteAsync(item, cancellationToken);
            ThrowIfFailed();
        }

        /// <summary>Completes the producer side and waits until all accepted work has finished.</summary>
        public async Task CompleteAndDrainAsync(CancellationToken cancellationToken)
        {
            Complete();
            Task workers = Task.WhenAll(_workers);
            await workers.WaitAsync(cancellationToken);
            ThrowIfFailed();
        }

        /// <summary>
        /// Stops accepting and processing work without draining it. This is intentionally used
        /// during monitor cancellation, where Helix cancellation takes precedence over uploads.
        /// </summary>
        public void Abandon()
        {
            Complete();
            _stop.Cancel();
        }

        public void Dispose()
        {
            Abandon();
            _stop.Dispose();
        }

        private async Task ConsumeAsync()
        {
            try
            {
                await foreach (T item in _channel.Reader.ReadAllAsync(_stop.Token))
                {
                    await _handler(item, _stop.Token);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                // The first worker failure terminates the whole pipeline. Producers and drainers
                // observe the same failure rather than continuing with a partially healthy queue.
                lock (_sync)
                {
                    _failure ??= ex;
                }

                _channel.Writer.TryComplete(ex);
                _stop.Cancel();
            }
        }

        private void Complete()
        {
            lock (_sync)
            {
                if (_completed)
                {
                    return;
                }

                _completed = true;
                _channel.Writer.TryComplete();
            }
        }

        private void ThrowIfFailed()
        {
            lock (_sync)
            {
                if (_failure is not null)
                {
                    throw new InvalidOperationException("An asynchronous work queue worker failed.", _failure);
                }
            }
        }
    }
}
