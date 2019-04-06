// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.GenFacades
{
    public class GenPartialFacadeSourceGenerator
    {
        public static bool Execute(
            string[] seeds,
            string referenceAssembly,
            string[] compileFiles,
            string defineConstants,
            string outputSourcePath,
            TaskLoggingHelper logger,
            bool ignoreMissingTypes = false,
            string[] ignoreMissingTypesList = null,
            string[] OmitTypes = null,
            ITaskItem[] seedTypePreferencesList = null)
        {
            var nameTable = new NameTable();
            var internFactory = new InternFactory();

            Dictionary<string, string> seedTypePreferences = ParseSeedTypePreferences(seedTypePreferencesList, logger);

            using (var contractHost = new HostEnvironment(nameTable, internFactory))
            using (var seedHost = new HostEnvironment(nameTable, internFactory))
            {
                IAssembly contractAssembly = contractHost.LoadAssembly(referenceAssembly);
                IEnumerable<string> referenceTypes = GetPublicVisibleTypes(contractAssembly);

                if (OmitTypes != null)
                    referenceTypes = referenceTypes.Where(type => !OmitTypes.Contains(type));

                IAssembly[] seedAssemblies = LoadAssemblies(seedHost, seeds).ToArray();
                var seedTypes = GenerateTypeTable(seedAssemblies);

                var sourceGenerator = new SourceGenerator(referenceTypes, seedTypes, seedTypePreferences, outputSourcePath, ignoreMissingTypesList, logger);
                return sourceGenerator.GenerateSource(compileFiles, ParseDefineConstants(defineConstants), ignoreMissingTypes);
            }
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

        private static IEnumerable<string> GetPublicVisibleTypes(IAssembly contractAssembly)
        {
            var typeForwardsToForward = contractAssembly.ExportedTypes.Select(alias => alias.AliasedType)
                                                                      .OfType<INamespaceTypeReference>();

            var typesToForward = contractAssembly.GetAllTypes().Where(t => TypeHelper.IsVisibleOutsideAssembly(t))
                                                               .OfType<INamespaceTypeDefinition>();

            return typeForwardsToForward.Concat(typesToForward)
                                        .Select(type => TypeHelper.GetTypeName(type, NameFormattingOptions.UseGenericTypeNameSuffix)).ToList();
        }

        private static void AddNestedTypesFromSeeds(List<string> types, INamedTypeDefinition type)
        {
            foreach (var nestedType in type.NestedTypes)
            {
                if (TypeHelper.IsVisibleOutsideAssembly(nestedType))
                    types.Add(TypeHelper.GetTypeName(nestedType));
                AddNestedTypesFromSeeds(types, nestedType);
            }
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> GenerateTypeTable(IEnumerable<IAssembly> seedAssemblies, IAssembly refAssembly = null)
        {
            var typeTable = new Dictionary<string, IReadOnlyList<INamedTypeDefinition>>();
            foreach (var assembly in seedAssemblies)
            {                
                bool internalsVisibleTo = refAssembly != null
                    ? UnitHelper.AssemblyOneAllowsAssemblyTwoToAccessItsInternals(assembly, refAssembly)
                    : false;

                foreach (var type in assembly.GetAllTypes().OfType<INamedTypeDefinition>())
                {         
                    if (internalsVisibleTo ? TypeHelper.IsVisibleToFriendAssemblies(type) : TypeHelper.IsVisibleOutsideAssembly(type))
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
                string typeName = TypeHelper.GetTypeName(type, NameFormattingOptions.UseGenericTypeNameSuffix);
                if (!typeTable.TryGetValue(typeName, out seedTypes))
                {
                    seedTypes = new List<INamedTypeDefinition>(1);
                    typeTable.Add(typeName, seedTypes);
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
