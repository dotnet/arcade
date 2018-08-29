// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Maestro.Contracts
{
    public interface ISubscriptionActor : IActor
    {
        Task UpdateAsync(int buildId);
        Task<string> CheckMergePolicyAsync(string prUrl);
        Task<string> RunAction(string action, object[] arguments);
    }
}
