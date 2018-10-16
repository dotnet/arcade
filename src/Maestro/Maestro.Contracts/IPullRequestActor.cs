using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Maestro.Contracts
{
    public interface IPullRequestActor : IActor
    {
        Task<string> RunActionAsync(string method, string arguments);
        Task UpdateAssetsAsync(Guid subscriptionId, int buildId, string sourceSha, List<Asset> assets);
    }
}
