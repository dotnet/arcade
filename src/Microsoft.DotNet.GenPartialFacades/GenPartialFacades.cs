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

namespace Microsoft.DotNet.GenPartialFacades
{
    public class GenPartialFacadesGenerator
    {
        public static bool Execute(
            string seeds,
            string contracts,
            string compileFiles,
            string contractAssemblyName,
            string constants,
            bool ignoreMissingTypes = false,
            string inclusionContracts = null,
            string[] seedTypePreferencesUnsplit = null,
            string [] typeAssemblyConversions = null,
            ErrorTreatment seedLoadErrorTreatment = ErrorTreatment.Default,
            ErrorTreatment contractLoadErrorTreatment = ErrorTreatment.Default)
        {
            var nameTable = new NameTable();
            var internFactory = new InternFactory();

            try
            {
                Dictionary<string, string> seedTypePreferences = ParseSeedTypePreferences(seedTypePreferencesUnsplit);
                Dictionary<string, string> assemblyTypePreferences = ParseAssemblyTypeAliases(typeAssemblyConversions);

                using (var contractHost = new HostEnvironment(nameTable, internFactory))
                using (var seedHost = new HostEnvironment(nameTable, internFactory))
                {
                    contractHost.LoadErrorTreatment = contractLoadErrorTreatment;
                    seedHost.LoadErrorTreatment = seedLoadErrorTreatment;

                    var contractAssemblies = LoadAssemblies(contractHost, contracts);
                    IReadOnlyDictionary<string, IEnumerable<string>> docIdTable = GenerateDocIdTable(contractAssemblies, inclusionContracts);

                    IAssembly[] seedAssemblies = LoadAssemblies(seedHost, seeds).ToArray();
                    var typeTable = GenerateTypeTable(seedAssemblies);

                    var facadeGenerator = new FacadeGenerator(docIdTable, typeTable, seedTypePreferences, assemblyTypePreferences);
                    return facadeGenerator.GenerateFacade(compileFiles.Split(";"), constants.Split(";"), ignoreMissingTypes, contractAssemblyName);
                }
            }
            catch (FacadeGenerationException ex)
            {
                Trace.TraceError(ex.Message);
#if !COREFX
                Debug.Assert(Environment.ExitCode != 0);
#endif
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
                        throw new FacadeGenerationException("Invalid seed type preference. Correct usage is /preferSeedType:FullTypeName=AssemblyName;");
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

        // This needs to be modified.
        private static Dictionary<string, string> ParseAssemblyTypeAliases(string[] alias)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);

            if (alias != null)
            {
                dictionary.Add("System.Private.Interop", "Alias_System_Private_Interop");
            }

            return dictionary;
        }

        private static IEnumerable<IAssembly> LoadAssemblies(HostEnvironment host, string assemblyPaths)
        {
            host.UnifyToLibPath = true;
            string[] splitPaths = HostEnvironment.SplitPaths(assemblyPaths);

            foreach (string path in splitPaths)
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

            return host.LoadAssemblies(splitPaths);
        }

        private static IReadOnlyDictionary<string, IEnumerable<string>> GenerateDocIdTable(IEnumerable<IAssembly> contractAssemblies, string inclusionContracts)
        {
            Dictionary<string, HashSet<string>> mutableDocIdTable = new Dictionary<string, HashSet<string>>();
            foreach (IAssembly contractAssembly in contractAssemblies)
            {
                string simpleName = contractAssembly.AssemblyIdentity.Name.Value;
                if (mutableDocIdTable.ContainsKey(simpleName))
                    throw new FacadeGenerationException(string.Format("Multiple contracts named \"{0}\" specified on -contracts.", simpleName));
                mutableDocIdTable[simpleName] = new HashSet<string>(EnumerateDocIdsToForward(contractAssembly));
            }

            if (inclusionContracts != null)
            {
                foreach (string inclusionContractPath in HostEnvironment.SplitPaths(inclusionContracts))
                {
                    // Assembly identity conflicts are permitted and normal in the inclusion contract list so load each one in a throwaway host to avoid problems.
                    using (HostEnvironment inclusionHost = new HostEnvironment(new NameTable(), new InternFactory()))
                    {
                        IAssembly inclusionAssembly = inclusionHost.LoadAssemblyFrom(inclusionContractPath);
                        if (inclusionAssembly == null || inclusionAssembly is Dummy)
                            throw new FacadeGenerationException(string.Format("Could not load assembly \"{0}\".", inclusionContractPath));
                        string simpleName = inclusionAssembly.Name.Value;
                        HashSet<string> hashset;
                        if (!mutableDocIdTable.TryGetValue(simpleName, out hashset))
                        {
                            Trace.TraceWarning("An assembly named \"{0}\" was specified in the -include list but no contract was specified named \"{0}\". Ignoring.", simpleName);
                        }
                        else
                        {
                            foreach (string docId in EnumerateDocIdsToForward(inclusionAssembly))
                            {
                                hashset.Add(docId);
                            }
                        }
                    }
                }
            }

            Dictionary<string, IEnumerable<string>> docIdTable = new Dictionary<string, IEnumerable<string>>();
            foreach (KeyValuePair<string, HashSet<string>> kv in mutableDocIdTable)
            {
                string key = kv.Key;
                IEnumerable<string> sortedDocIds = kv.Value.OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
                docIdTable.Add(key, sortedDocIds);
            }
            return docIdTable;
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
