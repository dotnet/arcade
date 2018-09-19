using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.DotNet.SwaggerGenerator.Modeler
{
    public class ServiceClientModel
    {
        public ServiceClientModel(
            string clientName,
            string @namespace,
            string host,
            string scheme,
            IEnumerable<TypeModel> types,
            IEnumerable<MethodGroupModel> methodGroups)
        {
            Name = clientName;
            Namespace = @namespace;
            Host = host;
            Scheme = scheme;
            Types = types.ToImmutableList();
            MethodGroups = methodGroups.ToImmutableList();
        }

        public ImmutableList<TypeModel> Types { get; }

        public IImmutableList<MethodGroupModel> MethodGroups { get; }

        public string Scheme { get; }

        public string Host { get; }

        public string Name { get; }
        public string Namespace { get; }
    }
}
