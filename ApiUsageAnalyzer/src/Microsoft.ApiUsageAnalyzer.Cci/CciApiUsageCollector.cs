using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.ApiUsageAnalyzer.Core;
using Microsoft.Cci;

namespace Microsoft.ApiUsageAnalyzer.Cci
{
    public class CciApiUsageCollector : IApiUsageCollector
    {
        public IImmutableList<(string api, int count)> GetApiUsage(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Could not load library", path);
            }
            var apis = new HashSet<string>();
            using (var host = new PeReader.DefaultHost())
            {
                IAssembly assembly = (IAssembly)host.LoadUnitFrom(path);
                foreach (var r in assembly.GetTypeReferences().OfType<object>().Concat(assembly.GetTypeMemberReferences()))
                {
                    switch (r)
                    {
                        case ITypeReference typeRef:
                            apis.Add(DocumentationIdHelper.GetId(typeRef));
                            break;
                        case IMethodReference methodRef:
                            apis.Add(DocumentationIdHelper.GetId(methodRef));
                            break;
                        case IFieldReference fieldRef:
                            apis.Add(DocumentationIdHelper.GetId(fieldRef));
                            break;
                        default:
                            throw new NotImplementedException(r?.GetType().Name ?? "null");
                    }
                }
            }
            return apis.Select(api => (api, 1)).ToImmutableList();
        }
    }
}
