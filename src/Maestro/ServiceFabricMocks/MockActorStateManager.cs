// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;

namespace ServiceFabricMocks
{
    public class MockActorStateManager : IActorStateManager
    {
        public readonly Dictionary<string, object> Data = new Dictionary<string, object>();

        public async Task AddStateAsync<T>(
            string stateName,
            T value,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (!await TryAddStateAsync(stateName, value, cancellationToken))
            {
                throw new InvalidOperationException($"State with name '{stateName}' already exists.");
            }
        }

        public Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.FromResult((T) Data[stateName]);
        }

        public Task SetStateAsync<T>(
            string stateName,
            T value,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Data[stateName] = value;
            return Task.CompletedTask;
        }

        public Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = new CancellationToken())
        {
            if (!Data.Remove(stateName))
            {
                Task.FromException(new KeyNotFoundException($"State with name '{stateName}' not found."));
            }

            return Task.CompletedTask;
        }

        public Task<bool> TryAddStateAsync<T>(
            string stateName,
            T value,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (!Data.ContainsKey(stateName))
            {
                Data[stateName] = value;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<ConditionalValue<T>> TryGetStateAsync<T>(
            string stateName,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (Data.TryGetValue(stateName, out object value))
            {
                return Task.FromResult(new ConditionalValue<T>(true, (T) value));
            }

            return Task.FromResult(new ConditionalValue<T>());
        }

        public Task<bool> TryRemoveStateAsync(
            string stateName,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.FromResult(Data.Remove(stateName));
        }

        public Task<bool> ContainsStateAsync(
            string stateName,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.FromResult(Data.ContainsKey(stateName));
        }

        public Task<T> GetOrAddStateAsync<T>(
            string stateName,
            T value,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (Data.ContainsKey(stateName))
            {
                return Task.FromResult((T) Data[stateName]);
            }

            Data[stateName] = value;
            return Task.FromResult(value);
        }

        public Task<T> AddOrUpdateStateAsync<T>(
            string stateName,
            T addValue,
            Func<string, T, T> updateValueFactory,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (Data.ContainsKey(stateName))
            {
                return Task.FromResult((T) (Data[stateName] = updateValueFactory(stateName, (T) Data[stateName])));
            }

            Data[stateName] = addValue;
            return Task.FromResult(addValue);
        }

        public Task<IEnumerable<string>> GetStateNamesAsync(
            CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.FromResult((IEnumerable<string>) Data.Keys);
        }

        public Task ClearCacheAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.CompletedTask;
        }

        public Task SaveStateAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.CompletedTask;
        }
    }
}
