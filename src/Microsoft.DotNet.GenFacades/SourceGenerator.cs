// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Cci;
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
        private readonly IEnumerable<string> _referenceTypes;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> _seedTypes;
        private readonly string _outputSourcePath;
        private readonly string[] _ignoreMissingTypesList;

        public SourceGenerator(
            IEnumerable<string> referenceTypes,
            IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> seedTypes,
            IReadOnlyDictionary<string, string> seedTypePreferences,
            string outputSourcePath,
            ITaskItem[] ignoreMissingTypesList
            )
        {
            _referenceTypes = referenceTypes;
            _seedTypes = seedTypes;
            _seedTypePreferences = seedTypePreferences;
            _outputSourcePath = outputSourcePath;
            _ignoreMissingTypesList = ignoreMissingTypesList?.Select(t => t.ItemSpec.ToString()).ToArray() ;
        }

        public bool GenerateSource(
            IEnumerable<string> compileFiles,
            IEnumerable<string> constants,
            bool ignoreMissingTypes)
        {
            List<string> externAliases = new List<string>();
            Dictionary<string, INamedTypeReference> forwardedTypes = new Dictionary<string, INamedTypeReference>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("#pragma warning disable CS0618");
            bool result = true;

            HashSet<string> existingTypes = compileFiles != null ? TypeParser.GetAllPublicTypes(compileFiles, constants) : null;

            IEnumerable<string> typesToForward = null;
            if (_referenceTypes != null)
            {
                typesToForward = compileFiles == null ? _referenceTypes : _referenceTypes.Where(id => !existingTypes.Contains(id));
            }
            else
            {
                typesToForward = existingTypes.ToList();
            }

            foreach (string type in typesToForward)
            {
                IReadOnlyList<INamedTypeDefinition> seedTypes;
                if (!_seedTypes.TryGetValue(type, out seedTypes))
                {
                    if (!ignoreMissingTypes && (_ignoreMissingTypesList == null || !_ignoreMissingTypesList.Contains(type)))
                    {
                        result = false;
                        Trace.TraceError("Did not find type '{0}' in any of the seed assemblies.", type);
                    }
                    continue;
                }

                string alias = "";
                if (seedTypes.Count > 1)
                {
                    if (_seedTypePreferences.Keys.Contains(type))
                    {
                        alias = _seedTypePreferences[type];
                        if (!externAliases.Contains(alias))
                            externAliases.Add(alias);
                    }
                    else
                    {
                        TraceDuplicateSeedTypeError(type, seedTypes);
                        result = false;
                        continue;
                    }
                }
                
                sb.AppendLine(GetTypeForwardsToString(type, alias));
            }
            sb.AppendLine("#pragma warning restore CS0618");
            File.WriteAllText(_outputSourcePath, AppendAliases(externAliases) + sb.ToString());
            return result;
        }

        private string AppendAliases(IEnumerable<string> externAliases)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string alias in externAliases)
            {
                sb.AppendLine(string.Format("extern alias {0};", alias));
            }

            return sb.ToString();
        }

        private static string GetTypeForwardsToString(string typeName, string alias = "")
        {
            if (typeName == "System.Void")
                typeName = "void";

            if (!string.IsNullOrEmpty(alias))
                alias += "::";

            return string.Format("[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof({0}{1}))]", alias, TransformGenericTypes(typeName));
        }

        private static string TransformGenericTypes(string typeName)
        {
            if (!typeName.Contains('`'))
                return typeName;

            StringBuilder sb = new StringBuilder();
            string[] stringParts = typeName.Split('`');
            sb.Append(stringParts[0]);

            for (int i = 0; i < stringParts.Length - 1; i++)
            {
                if (i != 0)
                {
                    sb.Append(stringParts[i].Substring(1));
                }

                int numberOfGenericParameters = int.Parse(stringParts[i + 1][0].ToString());

                sb.Append("<");
                sb.Append(',', numberOfGenericParameters - 1);
                sb.Append('>');
            }

            sb.Append(stringParts[stringParts.Length - 1].Substring(1));
            return sb.ToString();
        }

        private static void TraceDuplicateSeedTypeError(string docId, IReadOnlyList<INamedTypeDefinition> seedTypes)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("The type '{0}' is defined in multiple seed assemblies. If this is intentional, specify the alias for this type and project reference", docId);
            Trace.TraceError(sb.ToString());
        }
    }
}
