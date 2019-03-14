// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.GenFacades
{
    internal class SourceGenerator
    {
        private readonly IReadOnlyDictionary<string, string> _seedTypePreferences;
        private readonly IEnumerable<string> _docIds;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> _typeTable;
        private readonly string _outputSourcePath;

        public SourceGenerator(
            IEnumerable<string> docIdTable,
            IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> typeTable,
            IReadOnlyDictionary<string, string> seedTypePreferences,
            string outputSourcePath
            )
        {
            _docIds = docIdTable;
            _typeTable = typeTable;
            _seedTypePreferences = seedTypePreferences;
            _outputSourcePath = outputSourcePath;
        }

        public bool GenerateSource(
            IEnumerable<string> compileFiles,
            IEnumerable<string> constants,
            bool ignoreMissingTypes,
            string contractAssemblyName)
        {
            List<string> externAliases = new List<string>();
            Dictionary<string, INamedTypeReference> forwardedTypes = new Dictionary<string, INamedTypeReference>();
            StringBuilder sb = new StringBuilder();
            bool error = false;

            List<string> existingDocIds = TypeParser.GetAllTypes(compileFiles, constants);
            IEnumerable<string> docIdsToForward = _docIds.Where(id => !existingDocIds.Contains(id.Substring(2)));

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

                string alias = "";
                if (seedTypes.Count > 1)
                {
                    if (_seedTypePreferences.Keys.Contains(docId))
                    {
                        alias = _seedTypePreferences[docId];
                        if (!externAliases.Contains(alias))
                            externAliases.Add(alias);
                    }
                    else
                    {
                        TraceDuplicateSeedTypeError(docId, seedTypes);
                        error = true;
                        continue;
                    }
                }
                
                sb.Append(TypeParser.AddTypeForwardToStringBuilder(docId, alias));
            }

            File.WriteAllText(_outputSourcePath, AppendAliases(externAliases) + sb.ToString());
            return error;
        }

        private string AppendAliases(IEnumerable<string> externAliases)
        {
            string aliases = string.Empty;
            foreach (string alias in externAliases)
            {
                aliases += "extern alias " + alias + ";\n";
            }

            return aliases;
        }

        private static void TraceDuplicateSeedTypeError(string docId, IReadOnlyList<INamedTypeDefinition> seedTypes)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("The type '{0}' is defined in multiple seed assemblies. If this is intentional, specify one of the following arguments to choose the preferred seed type:", docId);

            foreach (INamedTypeDefinition type in seedTypes)
            {
                sb.AppendFormat("/preferSeedType:{0}={1}", docId.Substring("T:".Length), type.GetAssembly().Name.Value);
            }

            Trace.TraceError(sb.ToString());
        }
    }
}
