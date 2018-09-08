using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Maestro.MergePolicies
{
    public abstract class MergePolicy
    {
        public string Name
        {
            get
            {
                var name = GetType().Name;
                if (name.EndsWith(nameof(MergePolicy)))
                {
                    name = name.Substring(0, name.Length - nameof(MergePolicy).Length);
                }

                return name;
            }
        }

        public abstract string DisplayName { get; }

        public async Task<MergePolicyEvaluationResult> EvaluateAsync(MergePolicyEvaluationContext context)
        {
            using (context.Logger.BeginScope("Evaluating Merge Policy {policyName}", Name))
            {
                return await DoEvaluateAsync(context);
            }
        }

        protected abstract Task<MergePolicyEvaluationResult> DoEvaluateAsync(MergePolicyEvaluationContext context);
    }

    public class MergePolicyEvaluationContext
    {
        private static readonly IReadOnlyDictionary<string, JToken> s_emptyProperties =
            new ReadOnlyDictionary<string, JToken>(new Dictionary<string, JToken>());

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

        public MergePolicyEvaluationContext(string pullRequestUrl, IRemote darc, ILogger logger, Dictionary<string, JToken> properties)
        {
            PullRequestUrl = pullRequestUrl;
            Darc = darc;
            Logger = logger;
            Properties = properties != null ? new ReadOnlyDictionary<string, JToken>(properties) : s_emptyProperties;
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

    public class MergePolicyEvaluationResult
    {
        internal MergePolicyEvaluationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }
    }
}
