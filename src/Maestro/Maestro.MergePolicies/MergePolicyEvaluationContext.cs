// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Maestro.MergePolicies
{
    public class MergePolicyEvaluationContext
    {
        private static readonly IReadOnlyDictionary<string, JToken> s_emptyProperties =
            new ReadOnlyDictionary<string, JToken>(new Dictionary<string, JToken>());

        public MergePolicyEvaluationContext(
            string pullRequestUrl,
            IRemote darc,
            ILogger logger,
            Dictionary<string, JToken> properties)
        {
            PullRequestUrl = pullRequestUrl;
            Darc = darc;
            Logger = logger;
            Properties = properties != null ? new ReadOnlyDictionary<string, JToken>(properties) : s_emptyProperties;
        }

        public string PullRequestUrl { get; }
        public IRemote Darc { get; }
        public ILogger Logger { get; }
        public IReadOnlyDictionary<string, JToken> Properties { get; }

        public T Get<T>(string key)
        {
            if (!Properties.TryGetValue(key, out JToken value))
            {
                return default;
            }

            return value.ToObject<T>();
        }

        public MergePolicyEvaluationResult Success()
        {
            return new MergePolicyEvaluationResult(true, null);
        }

        public MergePolicyEvaluationResult Fail(string message)
        {
            return new MergePolicyEvaluationResult(false, message);
        }
    }
}
