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

namespace Microsoft.DotNet.GenFacades
{
    public class GenPartialFacadesGenerator
    {
        public static bool Execute(
            string[] seeds,
            string referenceAssembly,
            string[] compileFiles,
            string assemblyName,
            string defineConstants,
            string outputSourcePath,
            bool ignoreMissingTypes = false,
            string[] seedTypePreferencesUnsplit = null,
            ErrorTreatment seedLoadErrorTreatment = ErrorTreatment.Default,
            ErrorTreatment contractLoadErrorTreatment = ErrorTreatment.Default)
        {
            var nameTable = new NameTable();
            var internFactory = new InternFactory();

            try
            {
                Dictionary<string, string> seedTypePreferences = ParseSeedTypePreferences(seedTypePreferencesUnsplit);

                using (var contractHost = new HostEnvironment(nameTable, internFactory))
                using (var seedHost = new HostEnvironment(nameTable, internFactory))
                {
                    contractHost.LoadErrorTreatment = contractLoadErrorTreatment;
                    seedHost.LoadErrorTreatment = seedLoadErrorTreatment;

                    IAssembly contractAssembly = contractHost.LoadAssembly(referenceAssembly);
                    IEnumerable<string> docIdTable = EnumerateDocIdsToForward(contractAssembly);

                    IAssembly[] seedAssemblies = LoadAssemblies(seedHost, seeds).ToArray();
                    var typeTable = GenerateTypeTable(seedAssemblies);

                    var sourceGenerator = new SourceGenerator(docIdTable, typeTable, seedTypePreferences, outputSourcePath);
                    return sourceGenerator.GenerateSource(compileFiles, defineConstants?.Split(';'), ignoreMissingTypes, assemblyName);
                }
            }
            catch (FacadeGenerationException ex)
            {
                Trace.TraceError(ex.Message);
                Debug.Assert(Environment.ExitCode != 0);
                return false;
            }
        }

        private static Dictionary<string, string> ParseSeedTypePreferences(string[] preferences)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);

            if (preferences != null)
            {
                foreach (string preference in preferences)
                {
                    int i = preference.IndexOf('=');
                    if (i < 0)
                    {
                        throw new FacadeGenerationException("Invalid seed type preference. Correct usage is /preferSeedType:FullTypeName=AliasName;");
                    }

                    string key = preference.Substring(0, i);
                    string value = preference.Substring(i + 1);

                    if (!key.StartsWith("T:", StringComparison.Ordinal))
                    {
                        key = "T:" + key;
                    }

                    string existingValue;
                    if (dictionary.TryGetValue(key, out existingValue))
                    {
                        Trace.TraceWarning("Overriding /preferSeedType:{0}={1} with /preferSeedType:{2}={3}.", key, existingValue, key, value);
                    }

                    dictionary[key] = value;
                }
            }

            return dictionary;
        }

        private static IEnumerable<IAssembly> LoadAssemblies(HostEnvironment host, string[] assemblyPaths)
        {
            host.UnifyToLibPath = true;

            foreach (string path in assemblyPaths)
            {
                if (Directory.Exists(path))
                {
                    host.AddLibPath(Path.GetFullPath(path));
                }
                else if (File.Exists(path))
                {
                    host.AddLibPath(Path.GetDirectoryName(Path.GetFullPath(path)));
                }
            }

            return host.LoadAssemblies(assemblyPaths);
        }

        private static IEnumerable<string> EnumerateDocIdsToForward(IAssembly contractAssembly)
        {
            // Use INamedTypeReference instead of INamespaceTypeReference in order to also include nested
            // class type-forwards.
            var typeForwardsToForward = contractAssembly.ExportedTypes.Select(alias => alias.AliasedType)
                                                                      .OfType<INamedTypeReference>();

            var typesToForward = contractAssembly.GetAllTypes().Where(t => TypeHelper.IsVisibleOutsideAssembly(t))
                                                               .OfType<INamespaceTypeDefinition>();
            List<string> result = typeForwardsToForward.Concat(typesToForward)
                                        .Select(type => TypeHelper.GetTypeName(type, NameFormattingOptions.DocumentationId)).ToList();

            foreach (var type in typesToForward)
            {
                AddNestedTypeDocIds(result, type);
            }

            return result;
        }

        private static void AddNestedTypeDocIds(List<string> docIds, INamedTypeDefinition type)
        {
            foreach (var nestedType in type.NestedTypes)
            {
                if (TypeHelper.IsVisibleOutsideAssembly(nestedType))
                    docIds.Add(TypeHelper.GetTypeName(nestedType, NameFormattingOptions.DocumentationId));
                AddNestedTypeDocIds(docIds, nestedType);
            }
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> GenerateTypeTable(IEnumerable<IAssembly> seedAssemblies)
        {
            var typeTable = new Dictionary<string, IReadOnlyList<INamedTypeDefinition>>();
            foreach (var assembly in seedAssemblies)
            {
                foreach (var type in assembly.GetAllTypes().OfType<INamedTypeDefinition>())
                {
                    if (!TypeHelper.IsVisibleOutsideAssembly(type))
                        continue;
                    AddTypeAndNestedTypesToTable(typeTable, type);
                }
            }
            return typeTable;
        }

        private static void AddTypeAndNestedTypesToTable(Dictionary<string, IReadOnlyList<INamedTypeDefinition>> typeTable, INamedTypeDefinition type)
        {
            if (type != null)
            {
                IReadOnlyList<INamedTypeDefinition> seedTypes;
                string docId = TypeHelper.GetTypeName(type, NameFormattingOptions.DocumentationId);
                if (!typeTable.TryGetValue(docId, out seedTypes))
                {
                    seedTypes = new List<INamedTypeDefinition>(1);
                    typeTable.Add(docId, seedTypes);
                }
                if (!seedTypes.Contains(type))
                    ((List<INamedTypeDefinition>)seedTypes).Add(type);

                foreach (INestedTypeDefinition nestedType in type.NestedTypes)
                {
                    if (TypeHelper.IsVisibleOutsideAssembly(nestedType))
                        AddTypeAndNestedTypesToTable(typeTable, nestedType);
                }
            }
        }
    }
}
