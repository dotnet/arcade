// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Microsoft.ServiceFabric.Actors;

namespace Maestro.Contracts
{
    /// <summary>
    ///     A factory that creates <see cref="ActorId" /> instances for PullRequestActors
    /// </summary>
    public static class PullRequestActorId
    {
        /// <summary>
        ///     Creates an <see cref="ActorId" /> identifying the PullRequestActor responsible for pull requests for the
        ///     non-batched subscription
        ///     referenced by <see paramref="subscriptionId" />.
        /// </summary>
        public static ActorId Create(Guid subscriptionId)
        {
            return new ActorId(subscriptionId);
        }

        /// <summary>
        ///     Creates an <see cref="ActorId" /> identifying the PullRequestActor responsible for pull requests for all batched
        ///     subscriptions
        ///     targeting the (<see paramref="repository" />, <see paramref="branch" />) pair.
        /// </summary>
        public static ActorId Create(string repository, string branch)
        {
            return new ActorId(Encode(repository) + ":" + Encode(branch));
        }

        /// <summary>
        ///     Parses an <see cref="ActorId" /> created by <see cref="Create(string, string)" /> into the (repository, branch)
        ///     pair that created it.
        /// </summary>
        public static (string repository, string branch) Parse(ActorId id)
        {
            string str = id.GetStringId();
            if (string.IsNullOrEmpty(str))
            {
                throw new ArgumentException("Actor id must be a string kind", nameof(id));
            }

            int colonIndex = str.IndexOf(":", StringComparison.Ordinal);

            if (colonIndex == -1)
            {
                throw new ArgumentException("Actor id not in correct format", nameof(id));
            }

            string repository = Decode(str.Substring(0, colonIndex));
            string branch = Decode(str.Substring(colonIndex + 1));
            return (repository, branch);
        }

        private static string Encode(string repository)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(repository));
        }

        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }
}
