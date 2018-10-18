// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DarcLib;

namespace Maestro.MergePolicies
{
    public interface IMergePolicyEvaluationContext
    {
        IRemote Darc { get; }
        string PullRequestUrl { get; }
        void Succeed(string message);
        void Fail(string message);
        void Pending(string message);
    }
}
