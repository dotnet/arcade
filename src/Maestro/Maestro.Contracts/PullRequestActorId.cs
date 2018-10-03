using System;
using System.Text;
using Microsoft.ServiceFabric.Actors;

namespace Maestro.Contracts
{
    public static class PullRequestActorId
    {
        public static ActorId Create(Guid subscriptionId)
        {
            return new ActorId(subscriptionId);
        }

        public static ActorId Create(string repository, string branch)
        {
            return new ActorId(Encode(repository) + ":" + Encode(branch));
        }

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
