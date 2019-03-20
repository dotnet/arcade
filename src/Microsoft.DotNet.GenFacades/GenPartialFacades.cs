// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
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
            string defineConstants,
            string outputSourcePath,
            bool ignoreMissingTypes = false,
            ITaskItem[] seedTypePreferencesUnsplit = null,
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
                    IEnumerable<string> referenceTypes = EnumerateDocIdsToForward(contractAssembly);
                    IAssembly[] seedAssemblies = LoadAssemblies(seedHost, seeds).ToArray();
                    var seedTypes = GenerateTypeTable(seedAssemblies);

                    var sourceGenerator = new SourceGenerator(referenceTypes, seedTypes, seedTypePreferences, outputSourcePath);
                    return sourceGenerator.GenerateSource(compileFiles, ParseDefineConstants(defineConstants), ignoreMissingTypes);
                }
            }
            catch (FacadeGenerationException ex)
            {
                Trace.TraceError(ex.Message);
                Debug.Assert(Environment.ExitCode != 0);
                return false;
            }
        }

        private static IEnumerable<string> ParseDefineConstants(string defineConstants)
        {
            return defineConstants?.Split(';', ',').Where(t => !string.IsNullOrEmpty(t)).ToArray();
        }

        private static Dictionary<string, string> ParseSeedTypePreferences(ITaskItem[] preferences)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);

            if (preferences != null)
            {
                foreach (var item in preferences)
                {
                    string key = item.ItemSpec;
                    string value = item.GetMetadata("Aliases");
                    string existingValue;

                    if (dictionary.TryGetValue(key, out existingValue))
                    {
                        Trace.TraceWarning("Overriding SeedType{0} for type {1} with {2}", existingValue, key, value);
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
                                        .Select(type => TypeHelper.GetTypeName(type, NameFormattingOptions.UseGenericTypeNameSuffix)).ToList();

            return result;
        }

        private static void AddNestedTypeDocIds(List<string> docIds, INamedTypeDefinition type)
        {
            foreach (var nestedType in type.NestedTypes)
            {
                if (TypeHelper.IsVisibleOutsideAssembly(nestedType))
                    docIds.Add(TypeHelper.GetTypeName(nestedType));
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
                string docId = TypeHelper.GetTypeName(type, NameFormattingOptions.UseGenericTypeNameSuffix);
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
