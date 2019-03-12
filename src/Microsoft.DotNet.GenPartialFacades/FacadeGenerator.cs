// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.GenPartialFacades
{
    internal class FacadeGenerator
    {
        private readonly IReadOnlyDictionary<string, string> _seedTypePreferences;
        private readonly IReadOnlyDictionary<string, string> _assemblyTypePreferences;
        private readonly IReadOnlyDictionary<string, IEnumerable<string>> _docIdTable;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> _typeTable;

        public FacadeGenerator(
            IReadOnlyDictionary<string, IEnumerable<string>> docIdTable,
            IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> typeTable,
            IReadOnlyDictionary<string, string> seedTypePreferences,
           IReadOnlyDictionary<string, string> assemblyTypePreferences
            )
        {
            _docIdTable = docIdTable;
            _typeTable = typeTable;
            _seedTypePreferences = seedTypePreferences;
            _assemblyTypePreferences = assemblyTypePreferences;
        }

        public bool GenerateFacade(
            IEnumerable<string> compileFiles,
            IEnumerable<string> constants,
            bool ignoreMissingTypes,
            string contractAssemblyName)
        {
            IEnumerable<string> docIds = _docIdTable[contractAssemblyName];

            List<string> existingDocIds = TypeParser.GetAllTypes(compileFiles, constants);
            IEnumerable<string> docIdsToForward = docIds.Where(id => !existingDocIds.Contains(id));

            Dictionary<string, INamedTypeReference> forwardedTypes = new Dictionary<string, INamedTypeReference>();
            StringBuilder sb = new StringBuilder();
            bool error = false;

            foreach (string docId in docIdsToForward)
            {
                IReadOnlyList<INamedTypeDefinition> seedTypes;
                if (!_typeTable.TryGetValue(docId, out seedTypes))
                {
                    if (!ignoreMissingTypes)
                    {
                        error = true;
                        Trace.TraceError("Did not find type '{0}' in any of the seed assemblies.", docId);
                    }
                    continue;
                }

                INamedTypeDefinition seedType = GetSeedType(docId, seedTypes);
                if (seedType == null)
                {
                    TraceDuplicateSeedTypeError(docId, seedTypes);
                    error = true;
                    continue;
                }

                string alias = "";
                if (seedTypes.Count > 1)
                {                    
                    alias = _assemblyTypePreferences[seedType.GetAssembly().Name.Value];
                }
                
                TypeParser.AddTypeForwardToStringBuilder(sb, docId, alias);
            }

            File.WriteAllText(@"C:\git\abc.forwards.cs", sb.ToString());
            return error;
        }

        private INamedTypeDefinition GetSeedType(string docId, IReadOnlyList<INamedTypeDefinition> seedTypes)
        {
            Debug.Assert(seedTypes.Count != 0); // we should already have checked for non-existent types.

            if (seedTypes.Count == 1)
            {
                return seedTypes[0];
            }

            string preferredSeedAssembly;
            if (_seedTypePreferences.TryGetValue(docId, out preferredSeedAssembly))
            {
                return seedTypes.SingleOrDefault(t => String.Equals(t.GetAssembly().Name.Value, preferredSeedAssembly, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private static void TraceDuplicateSeedTypeError(string docId, IReadOnlyList<INamedTypeDefinition> seedTypes)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("The type '{0}' is defined in multiple seed assemblies. If this is intentional, specify one of the following arguments to choose the preferred seed type:", docId);

            foreach (INamedTypeDefinition type in seedTypes)
            {
                sb.AppendFormat("  /preferSeedType:{0}={1}", docId.Substring("T:".Length), type.GetAssembly().Name.Value);
            }

            Trace.TraceError(sb.ToString());
        }
    }

}
