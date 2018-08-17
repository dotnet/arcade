// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal class ActivityServiceRemotingClientFactory : IServiceRemotingClientFactory
    {
        private readonly IServiceRemotingClientFactory _inner;

        public ActivityServiceRemotingClientFactory(IServiceRemotingClientFactory inner)
        {
            _inner = inner;
        }

        public async Task<IServiceRemotingClient> GetClientAsync(
            Uri serviceUri,
            ServicePartitionKey partitionKey,
            TargetReplicaSelector targetReplicaSelector,
            string listenerName,
            OperationRetrySettings retrySettings,
            CancellationToken cancellationToken)
        {
            IServiceRemotingClient inner = await _inner.GetClientAsync(
                serviceUri,
                partitionKey,
                targetReplicaSelector,
                listenerName,
                retrySettings,
                cancellationToken);
            return new ActivityServiceRemotingClient(inner);
        }

        public async Task<IServiceRemotingClient> GetClientAsync(
            ResolvedServicePartition previousRsp,
            TargetReplicaSelector targetReplicaSelector,
            string listenerName,
            OperationRetrySettings retrySettings,
            CancellationToken cancellationToken)
        {
            IServiceRemotingClient inner = await _inner.GetClientAsync(
                previousRsp,
                targetReplicaSelector,
                listenerName,
                retrySettings,
                cancellationToken);
            return new ActivityServiceRemotingClient(inner);
        }

        public Task<OperationRetryControl> ReportOperationExceptionAsync(
            IServiceRemotingClient client,
            ExceptionInformation exceptionInformation,
            OperationRetrySettings retrySettings,
            CancellationToken cancellationToken)
        {
            IServiceRemotingClient innerClient = ((ActivityServiceRemotingClient) client)._inner;
            return _inner.ReportOperationExceptionAsync(
                innerClient,
                exceptionInformation,
                retrySettings,
                cancellationToken);
        }

        public event EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> ClientConnected
        {
            add => _inner.ClientConnected += value;
            remove => _inner.ClientConnected -= value;
        }

        public event EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> ClientDisconnected
        {
            add => _inner.ClientDisconnected += value;
            remove => _inner.ClientDisconnected -= value;
        }

        public IServiceRemotingMessageBodyFactory GetRemotingMessageBodyFactory()
        {
            return _inner.GetRemotingMessageBodyFactory();
        }
    }
}
