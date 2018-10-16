// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Maestro.MergePolicies
{
    public class MergePolicyProperties
    {
        public MergePolicyProperties(IReadOnlyDictionary<string, JToken> properties)
        {
            Properties = properties;
        }

        public IReadOnlyDictionary<string, JToken> Properties { get; }

        public T Get<T>(string key)
        {
            T result = default;
            if (Properties != null && Properties.TryGetValue(key, out JToken value))
            {
                result = value.ToObject<T>();
            }

            return result;
        }
    }

    public abstract class MergePolicy
    {
        public string Name
        {
            get
            {
                string name = GetType().Name;
                if (name.EndsWith(nameof(MergePolicy)))
                {
                    name = name.Substring(0, name.Length - nameof(MergePolicy).Length);
                }

                return name;
            }
        }

        public abstract string DisplayName { get; }

        public abstract Task EvaluateAsync(IMergePolicyEvaluationContext context, MergePolicyProperties properties);
    }
}
