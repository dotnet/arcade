// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace SubscriptionActorService
{
    public interface IActionTracker
    {
        Task TrackSuccessfulAction(string action, string result);

        Task TrackFailedAction(string action, string result, string method, string arguments);
    }
}
