// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.GenFacades
{
    public class GenPartialFacadeSourceGenerator
    {
        public static bool Execute(
            string[] seeds,
            string contractAssembly,
            string[] compileFiles,
            string defineConstants,
            string outputSourcePath,
            TaskLoggingHelper logger,
            bool ignoreMissingTypes = false,
            string[] ignoreMissingTypesList = null,
            string[] OmitTypes = null,
            ITaskItem[] seedTypePreferencesList = null)
        {
            Dictionary<string, string> seedTypePreferences = ParseSeedTypePreferences(seedTypePreferencesList, logger);

            IEnumerable<string> referenceTypes = GetPublicVisibleTypes(contractAssembly, includeTypeForwards: true);

            IReadOnlyDictionary<string, IReadOnlyList<string>> seedTypes = GenerateTypeTable(seeds);

            if (OmitTypes != null)
                referenceTypes = referenceTypes.Where(type => !OmitTypes.Contains(type));

            var sourceGenerator = new SourceGenerator(referenceTypes, seedTypes, seedTypePreferences, outputSourcePath, ignoreMissingTypesList, logger);
            return sourceGenerator.GenerateSource(compileFiles, ParseDefineConstants(defineConstants), ignoreMissingTypes);
        }

        private static IEnumerable<string> ParseDefineConstants(string defineConstants)
        {
            return defineConstants?.Split(';', ',').Where(t => !string.IsNullOrEmpty(t)).ToArray();
        }

        private static Dictionary<string, string> ParseSeedTypePreferences(ITaskItem[] preferences, TaskLoggingHelper logger)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);

            if (preferences != null)
            {
                foreach (var item in preferences)
                {
                    string key = item.ItemSpec;
                    string value = item.GetMetadata("Aliases");

                    if (value != null)
                    {
                        string existingValue;

                        if (dictionary.TryGetValue(key, out existingValue))
                        {
                            logger.LogWarning($"Overriding SeedType{existingValue} for type {key} with {value}");
                        }

                        dictionary[key] = value;
                    }
                    else
                    {
                        logger.LogWarning($"No Alias has been provided for type {key}");
                    }
                }
            }

            return dictionary;
        }

        private static IEnumerable<string> GetPublicVisibleTypes(string assembly, bool includeTypeForwards = false)
        {
            using (var peReader = new PEReader(new FileStream(assembly, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read)))
            {
                if (peReader.HasMetadata)
                {
                    MetadataReader reader = peReader.GetMetadataReader();

                    // Enumerating typeDefinatons
                    foreach (var typeDefinationHandle in reader.TypeDefinitions)
                    {
                        TypeDefinition typeDefination = reader.GetTypeDefinition(typeDefinationHandle);
                        string typeName = reader.GetString(typeDefination.Name);

                        // Ignoring Nested types
                        if (typeName != "<Module>" && !typeDefination.Namespace.IsNil && CheckTypeVisibility(typeDefination))
                        {
                            string namespaceName = reader.GetString(typeDefination.Namespace);
                            yield return namespaceName + "." + typeName;
                        }
                    }
                    
                    if (includeTypeForwards)
                    {
                        // Enumerating typeforwards
                        foreach (var exportedTypeHandle in reader.ExportedTypes)
                        {
                            var exportedType = reader.GetExportedType(exportedTypeHandle);
                            if (exportedType.IsForwarder && !exportedType.Namespace.IsNil)
                                yield return reader.GetString(exportedType.Namespace) + "." + reader.GetString(exportedType.Name);
                        }
                    }
                }
            }
        }

        // This is added to remove compiler generated internal types.
        private static bool CheckTypeVisibility(TypeDefinition typeDefination)
        {
            return (typeDefination.Attributes & TypeAttributes.Public) != 0;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> GenerateTypeTable(IEnumerable<string> seedAssemblies)
        {
            var typeTable = new Dictionary<string, IReadOnlyList<string>>();
            foreach(string assembly in seedAssemblies)
            {                
                IEnumerable<string> types = GetPublicVisibleTypes(assembly);
                foreach (string type in types)
                {
                    AddTypeToTable(typeTable, type, assembly);
                }
            }
            return typeTable;
        }

        private static void AddTypeToTable(Dictionary<string, IReadOnlyList<string>> typeTable, string type, string assembly)
        {
            if (type != null)
            {
                IReadOnlyList<string> seedTypes;                
                if (!typeTable.TryGetValue(type, out seedTypes))
                {
                    seedTypes = new List<string>(1);
                    typeTable.Add(type, seedTypes);
                }
                if (!seedTypes.Contains(type))
                    ((List<string>)seedTypes).Add(assembly);
            }
        }
    }
}
