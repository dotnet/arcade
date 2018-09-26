// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.MutableCodeModel;
using System.Globalization;
using Microsoft.DiaSymReader.Tools;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.GenFacades
{
    public class Generator
    {
        private const uint ReferenceAssemblyFlag = 0x70;

        public static bool Execute(
            string seeds,
            string contracts,
            string facadePath,
            Version assemblyFileVersion = null,
            bool clearBuildAndRevision = false,
            bool ignoreMissingTypes = false,
            bool ignoreBuildAndRevisionMismatch = false,
            bool buildDesignTimeFacades = false,
            string inclusionContracts = null,
            ErrorTreatment seedLoadErrorTreatment = ErrorTreatment.Default,
            ErrorTreatment contractLoadErrorTreatment = ErrorTreatment.Default,
            string[] seedTypePreferencesUnsplit = null,
            bool forceZeroVersionSeeds = false,
            bool producePdb = true,
            string partialFacadeAssemblyPath = null,
            bool buildPartialReferenceFacade = false)
        {
            if (!Directory.Exists(facadePath))
                Directory.CreateDirectory(facadePath);

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

                    var contractAssemblies = LoadAssemblies(contractHost, contracts);
                    IReadOnlyDictionary<string, IEnumerable<string>> docIdTable = GenerateDocIdTable(contractAssemblies, inclusionContracts);

                    IAssembly[] seedAssemblies = LoadAssemblies(seedHost, seeds).ToArray();

                    IAssemblyReference seedCoreAssemblyRef = ((Microsoft.Cci.Immutable.PlatformType)seedHost.PlatformType).CoreAssemblyRef;

                    if (forceZeroVersionSeeds)
                    {
                        // Create a deep copier, copy the seed assemblies, and zero out their versions.
                        var copier = new MetadataDeepCopier(seedHost);

                        for (int i = 0; i < seedAssemblies.Length; i++)
                        {
                            var mutableSeed = copier.Copy(seedAssemblies[i]);
                            mutableSeed.Version = new Version(0, 0, 0, 0);
                            // Copy the modified seed assembly back.
                            seedAssemblies[i] = mutableSeed;

                            if (mutableSeed.Name.UniqueKey == seedCoreAssemblyRef.Name.UniqueKey)
                            {
                                seedCoreAssemblyRef = mutableSeed;
                            }
                        }
                    }

                    var typeTable = GenerateTypeTable(seedAssemblies);
                    var facadeGenerator = new FacadeGenerator(seedHost, contractHost, docIdTable, typeTable, seedTypePreferences, clearBuildAndRevision, buildDesignTimeFacades, assemblyFileVersion);

                    if (buildPartialReferenceFacade && ignoreMissingTypes)
                    {
                        throw new FacadeGenerationException(
                            "When buildPartialReferenceFacade is specified ignoreMissingTypes must not be specified.");
                    }

                    if (partialFacadeAssemblyPath != null)
                    {
                        if (contractAssemblies.Count() != 1)
                        {
                            throw new FacadeGenerationException(
                                "When partialFacadeAssemblyPath is specified, only exactly one corresponding contract assembly can be specified.");
                        }

                        if (buildPartialReferenceFacade)
                        {
                            throw new FacadeGenerationException(
                                "When partialFacadeAssemblyPath is specified, buildPartialReferenceFacade must not be specified.");
                        }

                        IAssembly contractAssembly = contractAssemblies.First();
                        IAssembly partialFacadeAssembly = seedHost.LoadAssembly(partialFacadeAssemblyPath);
                        if (contractAssembly.Name != partialFacadeAssembly.Name
                            || contractAssembly.Version.Major != partialFacadeAssembly.Version.Major
                            || contractAssembly.Version.Minor != partialFacadeAssembly.Version.Minor
                            || (!ignoreBuildAndRevisionMismatch && contractAssembly.Version.Build != partialFacadeAssembly.Version.Build)
                            || (!ignoreBuildAndRevisionMismatch && contractAssembly.Version.Revision != partialFacadeAssembly.Version.Revision)
                            || contractAssembly.GetPublicKeyToken() != partialFacadeAssembly.GetPublicKeyToken())
                        {
                            throw new FacadeGenerationException(
                                string.Format("The partial facade assembly's name, version, and public key token must exactly match the contract to be filled. Contract: {0}, Facade: {1}",
                                    contractAssembly.AssemblyIdentity,
                                    partialFacadeAssembly.AssemblyIdentity));
                        }

                        Assembly filledPartialFacade = facadeGenerator.GenerateFacade(contractAssembly, seedCoreAssemblyRef, ignoreMissingTypes, 
                            overrideContractAssembly: partialFacadeAssembly, 
                            forceAssemblyReferenceVersionsToZero: forceZeroVersionSeeds);

                        if (filledPartialFacade == null)
                        {
                            Trace.TraceError("Errors were encountered while generating the facade.");
                            return false;
                        }

                        string pdbLocation = null;

                        if (producePdb)
                        {
                            string pdbFolder = Path.GetDirectoryName(partialFacadeAssemblyPath);
                            pdbLocation = Path.Combine(pdbFolder, contractAssembly.Name + ".pdb");
                            if (producePdb && !File.Exists(pdbLocation))
                            {
                                pdbLocation = null;
                                Trace.TraceWarning("No PDB file present for un-transformed partial facade. No PDB will be generated.");
                            }
                        }

                        OutputFacadeToFile(facadePath, seedHost, filledPartialFacade, contractAssembly, pdbLocation);
                    }
                    else
                    {
                        foreach (var contract in contractAssemblies)
                        {
                            Assembly facade = facadeGenerator.GenerateFacade(contract, seedCoreAssemblyRef, ignoreMissingTypes, buildPartialReferenceFacade: buildPartialReferenceFacade);
                            if (facade == null)
                            {
#if !COREFX
                                Debug.Assert(Environment.ExitCode != 0);
#endif
                                return false;
                            }

                            OutputFacadeToFile(facadePath, seedHost, facade, contract);
                        }
                    }
                }

                return true;
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

        private static void OutputFacadeToFile(string facadePath, HostEnvironment seedHost, Assembly facade, IAssembly contract, string pdbLocation = null)
        {
            bool needsConversion = false;
            string pdbOutputPath = Path.Combine(facadePath, contract.Name + ".pdb");
            string finalPdbOutputPath = pdbOutputPath;

            // Use the filename (including extension .dll/.winmd) so people can have some control over the output facade file name.
            string facadeFileName = Path.GetFileName(contract.Location);
            string facadeOutputPath = Path.Combine(facadePath, facadeFileName);
            using (Stream peOutStream = File.Create(facadeOutputPath))
            {
                if (pdbLocation != null)
                {
                    if (File.Exists(pdbLocation))
                    {
                        // Convert from portable to windows PDBs if necessary.  If we convert
                        // set the pdbOutput path to a *.windows.pdb file so we can convert it back 
                        // to 'finalPdbOutputPath'.
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            needsConversion = ConvertFromPortableIfNecessary(facade.Location, ref pdbLocation);
                        if (needsConversion)
                        {                
                            // We want to keep the same file name for the PDB because it is used as a key when looking it up on a symbol server 
                            string pdbOutputPathPdbDir = Path.Combine(Path.GetDirectoryName(pdbOutputPath), "WindowsPdb");
                            Directory.CreateDirectory(pdbOutputPathPdbDir);
                            pdbOutputPath = Path.Combine(pdbOutputPathPdbDir, Path.GetFileName(pdbOutputPath));
                        }

                        // do the main GenFacades logic (which today only works with windows PDBs). 
                        using (Stream pdbReadStream = File.OpenRead(pdbLocation))
                        using (PdbReader pdbReader = new PdbReader(pdbReadStream, seedHost))
                        using (PdbWriter pdbWriter = new PdbWriter(pdbOutputPath, pdbReader))
                        {
                            PeWriter.WritePeToStream(facade, seedHost, peOutStream, pdbReader, pdbReader, pdbWriter);
                        }
                    }
                    else
                    {
                        throw new FacadeGenerationException("Couldn't find the pdb at the given location: " + pdbLocation);
                    }
                }
                else
                {
                    PeWriter.WritePeToStream(facade, seedHost, peOutStream);
                }
            }

            // If we started with Portable PDBs we need to convert the output to portable again. 
            // We have to do this after facadeOutputPath is closed for writing.   
            if (needsConversion)
            {
                Trace.TraceInformation("Converting PDB generated by GenFacades " + pdbOutputPath + " to portable format " + finalPdbOutputPath);
                ConvertFromWindowsPdb(facadeOutputPath, pdbOutputPath, finalPdbOutputPath);
            }
        }

        /// <summary>
        /// Given dllInputPath determine if it is portable.  If so convert it *\WindowsPdb\*.pdb and update 'pdbInputPath' to 
        /// point to this converted file.
        /// Returns true if the conversion was done (that is the original file was protable).  
        /// 'dllInputPath' is the DLL that goes along with 'pdbInputPath'.  
        /// </summary>
        private static bool ConvertFromPortableIfNecessary(string dllInputPath, ref string pdbInputPath)
        {
            string originalPdbInputPath = pdbInputPath;
            using (Stream pdbReadStream = File.OpenRead(pdbInputPath))
            {
                // If the input is not portable, there is nothing to do, and we can early out.  
                if (!PdbConverter.IsPortable(pdbReadStream))
                    return false;

                // We want to keep the same file name for the PDB because it is used as a key when looking it up on a symbol server 
                string pdbInputPathPdbDir = Path.Combine(Path.GetDirectoryName(pdbInputPath), "WindowsPdb");
                Directory.CreateDirectory(pdbInputPathPdbDir);
                pdbInputPath =  Path.Combine(pdbInputPathPdbDir, Path.GetFileName(pdbInputPath)); 

                Trace.TraceInformation("PDB " + originalPdbInputPath + " is a portable PDB, converting it to " + pdbInputPath);
                PdbConverter converter = new PdbConverter(d => Trace.TraceError(d.ToString(CultureInfo.InvariantCulture)));
                using (Stream peStream = File.OpenRead(dllInputPath))
                using (Stream pdbWriteStream = File.OpenWrite(pdbInputPath))
                {
                    converter.ConvertPortableToWindows(peStream, pdbReadStream, pdbWriteStream, new PortablePdbConversionOptions(suppressSourceLinkConversion: true));
                }
            }
            return true;
        }

        /// <summary>
        /// Convert the windows PDB winPdbInputPath to portablePdbOutputPath. 
        /// 'dllInputPath' is the DLL that goes along with 'winPdbInputPath'.  
        /// </summary>
        private static void ConvertFromWindowsPdb(string dllInputPath, string winPdbInputPath, string portablePdbOutputPath)
        {
            PdbConverter converter = new PdbConverter(d => Trace.TraceError(d.ToString(CultureInfo.InvariantCulture)));
            using (Stream peStream = File.OpenRead(dllInputPath))
            using (Stream pdbReadStream = File.OpenRead(winPdbInputPath))
            using (Stream pdbWriteStream = File.OpenWrite(portablePdbOutputPath))
            {
                converter.ConvertWindowsToPortable(peStream, pdbReadStream, pdbWriteStream);
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
                        throw new FacadeGenerationException("Invalid seed type preference. Correct usage is /preferSeedType:FullTypeName=AssemblyName");
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

            foreach(var type in typesToForward)
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

        private class FacadeGenerator
        {
            private readonly IMetadataHost _seedHost;
            private readonly IMetadataHost _contractHost;
            private readonly IReadOnlyDictionary<string, IEnumerable<string>> _docIdTable;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> _typeTable;
            private readonly IReadOnlyDictionary<string, string> _seedTypePreferences;
            private readonly bool _clearBuildAndRevision;
            private readonly bool _buildDesignTimeFacades;
            private readonly Version _assemblyFileVersion;

            public FacadeGenerator(
                IMetadataHost seedHost,
                IMetadataHost contractHost,
                IReadOnlyDictionary<string, IEnumerable<string>> docIdTable,
                IReadOnlyDictionary<string, IReadOnlyList<INamedTypeDefinition>> typeTable,
                IReadOnlyDictionary<string, string> seedTypePreferences,
                bool clearBuildAndRevision,
                bool buildDesignTimeFacades,
                Version assemblyFileVersion
                )
            {
                _seedHost = seedHost;
                _contractHost = contractHost;
                _docIdTable = docIdTable;
                _typeTable = typeTable;
                _seedTypePreferences = seedTypePreferences;
                _clearBuildAndRevision = clearBuildAndRevision;
                _buildDesignTimeFacades = buildDesignTimeFacades;
                _assemblyFileVersion = assemblyFileVersion;
            }

            public Assembly GenerateFacade(IAssembly contractAssembly, 
                IAssemblyReference seedCoreAssemblyReference, 
                bool ignoreMissingTypes, IAssembly overrideContractAssembly = null,
                bool buildPartialReferenceFacade = false, 
                bool forceAssemblyReferenceVersionsToZero = false)
            {
                Assembly assembly;
                if (overrideContractAssembly != null)
                {
                    MetadataDeepCopier copier = new MetadataDeepCopier(_seedHost);
                    assembly = copier.Copy(overrideContractAssembly); // Use non-empty partial facade if present
                }
                else
                {
                    MetadataDeepCopier copier = new MetadataDeepCopier(_contractHost);
                    assembly = copier.Copy(contractAssembly);

                    // if building a reference facade don't strip the contract
                    if (!buildPartialReferenceFacade)
                    {
                        ReferenceAssemblyToFacadeRewriter rewriter = new ReferenceAssemblyToFacadeRewriter(_seedHost, _contractHost, seedCoreAssemblyReference, _assemblyFileVersion != null);
                        rewriter.Rewrite(assembly);
                    }
                }

                if (forceAssemblyReferenceVersionsToZero)
                {
                    foreach (AssemblyReference ar in assembly.AssemblyReferences)
                    {
                        ar.Version = new Version(0, 0, 0, 0);
                    }
                }

                string contractAssemblyName = contractAssembly.AssemblyIdentity.Name.Value;
                IEnumerable<string> docIds = _docIdTable[contractAssemblyName];

                // Add all the type forwards
                bool error = false;

                Dictionary<string, INamedTypeDefinition> existingDocIds = assembly.AllTypes.ToDictionary(typeDef => typeDef.RefDocId(), typeDef => typeDef);
                IEnumerable<string> docIdsToForward = buildPartialReferenceFacade ? existingDocIds.Keys : docIds.Where(id => !existingDocIds.ContainsKey(id));
                Dictionary<string, INamedTypeReference> forwardedTypes = new Dictionary<string, INamedTypeReference>();
                foreach (string docId in docIdsToForward)
                {
                    IReadOnlyList<INamedTypeDefinition> seedTypes;
                    if (!_typeTable.TryGetValue(docId, out seedTypes))
                    {
                        if (!ignoreMissingTypes && !buildPartialReferenceFacade)
                        {
                            Trace.TraceError("Did not find type '{0}' in any of the seed assemblies.", docId);
                            error = true;
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

                    if (buildPartialReferenceFacade)
                    {
                        // honor preferSeedType for keeping contract type
                        string preferredSeedAssembly;
                        bool keepType = _seedTypePreferences.TryGetValue(docId, out preferredSeedAssembly) &&
                            contractAssemblyName.Equals(preferredSeedAssembly, StringComparison.OrdinalIgnoreCase);

                        if (keepType)
                        {
                            continue;
                        }

                        assembly.AllTypes.Remove(existingDocIds[docId]);
                        forwardedTypes.Add(docId, seedType);
                    }

                    AddTypeForward(assembly, seedType);
                }

                if (buildPartialReferenceFacade)
                {
                    if (forwardedTypes.Count == 0)
                    {
                        Trace.TraceError("Did not find any types in any of the seed assemblies.");
                        return null;
                    }
                    else
                    {
                        // for any thing that's now a typeforward, make sure typerefs point to that rather than
                        // the type previously inside the assembly.
                        TypeReferenceRewriter typeRefRewriter = new TypeReferenceRewriter(_seedHost, oldType =>
                        {
                            INamedTypeReference newType = null;
                            return forwardedTypes.TryGetValue(oldType.DocId(), out newType) ? newType : oldType;
                        });

                        var remainingTypes = assembly.AllTypes.Where(t => t.Name.Value != "<Module>");

                        if (!remainingTypes.Any())
                        {
                            Trace.TraceInformation($"Removed all types from {contractAssembly.Name} thus will remove ReferenceAssemblyAttribute.");
                            assembly.AssemblyAttributes.RemoveAll(ca => ca.FullName() == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute");
                            assembly.Flags &= ~ReferenceAssemblyFlag;
                        }

                        typeRefRewriter.Rewrite(assembly);
                    }
                }

                if (error)
                {
                    return null;
                }

                if (_assemblyFileVersion != null)
                {
                    assembly.AssemblyAttributes.Add(CreateAttribute("System.Reflection.AssemblyFileVersionAttribute", seedCoreAssemblyReference.ResolvedAssembly, _assemblyFileVersion.ToString()));
                    assembly.AssemblyAttributes.Add(CreateAttribute("System.Reflection.AssemblyInformationalVersionAttribute", seedCoreAssemblyReference.ResolvedAssembly, _assemblyFileVersion.ToString()));
                }

                if (_buildDesignTimeFacades)
                {
                    assembly.AssemblyAttributes.Add(CreateAttribute("System.Runtime.CompilerServices.ReferenceAssemblyAttribute", seedCoreAssemblyReference.ResolvedAssembly));
                    assembly.Flags |= ReferenceAssemblyFlag;
                }

                if (_clearBuildAndRevision)
                {
                    assembly.Version = new Version(assembly.Version.Major, assembly.Version.Minor, 0, 0);
                }

                AddWin32VersionResource(contractAssembly.Location, assembly);

                return assembly;
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

            private void AddTypeForward(Assembly assembly, INamedTypeDefinition seedType)
            {
                var alias = new NamespaceAliasForType();
                alias.AliasedType = ConvertDefinitionToReferenceIfTypeIsNested(seedType, _seedHost);
                alias.IsPublic = true;

                if (assembly.ExportedTypes == null)
                    assembly.ExportedTypes = new List<IAliasForType>();
                // Make sure that the typeforward doesn't already exist in the ExportedTypes
                if (!assembly.ExportedTypes.Any(t => t.AliasedType.RefDocId() == alias.AliasedType.RefDocId()))
                    assembly.ExportedTypes.Add(alias);
                else
                    throw new FacadeGenerationException($"{seedType.FullName()} typeforward already exists");
            }

            private void AddWin32VersionResource(string contractLocation, Assembly facade)
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(contractLocation);
                Version fileVersion = new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart, versionInfo.FileBuildPart, versionInfo.FilePrivatePart);
                Version productVersion = new Version(versionInfo.ProductMajorPart, versionInfo.ProductMinorPart, versionInfo.ProductBuildPart, versionInfo.ProductPrivatePart);
                var versionSerializer = new VersionResourceSerializer(
                    true,
                    versionInfo.Comments,
                    versionInfo.CompanyName,
                    versionInfo.FileDescription,
                    fileVersion,
                    versionInfo.InternalName,
                    versionInfo.LegalCopyright,
                    versionInfo.LegalTrademarks,
                    versionInfo.OriginalFilename,
                    versionInfo.ProductName,
                    productVersion,
                    facade.Version);

                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, Encoding.Unicode, true))
                {
                    versionSerializer.WriteVerResource(writer);

                    var resource = new Win32Resource();
                    resource.Id = 1;
                    resource.TypeId = 0x10;
                    resource.Data = stream.ToArray().ToList();

                    facade.Win32Resources.Add(resource);
                }
            }

            // This shouldn't be necessary, but CCI is putting a nonzero TypeDefId in the ExportedTypes table
            // for nested types if NamespaceAliasForType.AliasedType is set to an ITypeDefinition
            // so we make an ITypeReference copy as a workaround.
            private static INamedTypeReference ConvertDefinitionToReferenceIfTypeIsNested(INamedTypeDefinition typeDef, IMetadataHost host)
            {
                var nestedTypeDef = typeDef as INestedTypeDefinition;
                if (nestedTypeDef == null)
                    return typeDef;

                var typeRef = new NestedTypeReference();
                typeRef.Copy(nestedTypeDef, host.InternFactory);
                return typeRef;
            }

            private ICustomAttribute CreateAttribute(string typeName, IAssembly seedCoreAssembly, string argument = null)
            {
                var type = seedCoreAssembly.GetAllTypes().FirstOrDefault(t => t.FullName() == typeName);
                if (type == null)
                {
                    throw new FacadeGenerationException(String.Format("Cannot find {0} type in seed core assembly.", typeName));
                }

                IEnumerable<IMethodDefinition> constructors = type.GetMembersNamed(_seedHost.NameTable.Ctor, false).OfType<IMethodDefinition>();

                IMethodDefinition constructor = null;
                if (argument != null)
                {
                    constructor = constructors.SingleOrDefault(m => m.ParameterCount == 1 && m.Parameters.First().Type.AreEquivalent("System.String"));
                }
                else
                {
                    constructor = constructors.SingleOrDefault(m => m.ParameterCount == 0);
                }

                if (constructor == null)
                {
                    throw new FacadeGenerationException(String.Format("Cannot find {0} constructor taking single string argument in seed core assembly.", typeName));
                }

                var attribute = new CustomAttribute();
                attribute.Constructor = constructor;

                if (argument != null)
                {
                    var argumentExpression = new MetadataConstant();
                    argumentExpression.Type = _seedHost.PlatformType.SystemString;
                    argumentExpression.Value = argument;

                    attribute.Arguments = new List<IMetadataExpression>(1);
                    attribute.Arguments.Add(argumentExpression);
                }

                return attribute;
            }
        }

        private class ReferenceAssemblyToFacadeRewriter : MetadataRewriter
        {
            private IMetadataHost _seedHost;
            private IMetadataHost _contractHost;
            private IAssemblyReference _seedCoreAssemblyReference;
            private bool _stripFileVersionAttributes;

            public ReferenceAssemblyToFacadeRewriter(
                IMetadataHost seedHost,
                IMetadataHost contractHost,
                IAssemblyReference seedCoreAssemblyReference,
                bool stripFileVersionAttributes)
                : base(seedHost)
            {
                _seedHost = seedHost;
                _contractHost = contractHost;
                _stripFileVersionAttributes = stripFileVersionAttributes;
                _seedCoreAssemblyReference = seedCoreAssemblyReference;
            }

            public override IAssemblyReference Rewrite(IAssemblyReference assemblyReference)
            {
                if (assemblyReference == null)
                    return assemblyReference;

                if (assemblyReference.UnifiedAssemblyIdentity.Equals(_contractHost.CoreAssemblySymbolicIdentity) &&
                    !assemblyReference.ModuleIdentity.Equals(host.CoreAssemblySymbolicIdentity))
                {
                    assemblyReference = _seedCoreAssemblyReference;
                }

                return base.Rewrite(assemblyReference);
            }

            public override void RewriteChildren(RootUnitNamespace rootUnitNamespace)
            {
                var assemblyReference = rootUnitNamespace.Unit as IAssemblyReference;
                if (assemblyReference != null)
                    rootUnitNamespace.Unit = Rewrite(assemblyReference).ResolvedUnit;

                base.RewriteChildren(rootUnitNamespace);
            }

            public override List<INamespaceMember> Rewrite(List<INamespaceMember> namespaceMembers)
            {
                // Ignore traversing or rewriting any namspace members.
                return base.Rewrite(new List<INamespaceMember>());
            }

            public override void RewriteChildren(Assembly assembly)
            {
                // Clear all win32 resources. The version resource will get repopulated.
                assembly.Win32Resources = new List<IWin32Resource>();

                // Remove all the references they will get repopulated while outputing.
                assembly.AssemblyReferences.Clear();

                // Remove all the module references (aka native references)
                assembly.ModuleReferences = new List<IModuleReference>();

                // Remove all file references (ex: *.nlp files in mscorlib)
                assembly.Files = new List<IFileReference>();

                // Remove all security attributes (ex: permissionset in IL)
                assembly.SecurityAttributes = new List<ISecurityAttribute>();

                // Reset the core assembly symbolic identity to the seed core assembly (e.g. mscorlib, corefx)
                // and not the contract core (e.g. System.Runtime).
                assembly.CoreAssemblySymbolicIdentity = _seedCoreAssemblyReference.AssemblyIdentity;

                // Add reference to seed core assembly up-front so that we keep the same order as the C# compiler.
                assembly.AssemblyReferences.Add(_seedCoreAssemblyReference);

                // Remove all type definitions except for the "<Module>" type. Remove all fields and methods from it.
                NamespaceTypeDefinition moduleType = assembly.AllTypes.SingleOrDefault(t => t.Name.Value == "<Module>") as NamespaceTypeDefinition;
                assembly.AllTypes.Clear();
                if (moduleType != null)
                {
                    moduleType.Fields?.Clear();
                    moduleType.Methods?.Clear();
                    assembly.AllTypes.Add(moduleType);
                }

                // Remove any preexisting typeforwards.
                assembly.ExportedTypes = new List<IAliasForType>();

                // Remove any preexisting resources.
                assembly.Resources = new List<IResourceReference>();

                // Clear the reference assembly flag from the contract.
                // For design-time facades, it will be added back later.
                assembly.Flags &= ~ReferenceAssemblyFlag;

                // This flag should not be set until the delay-signed assembly we emit is actually signed.
                assembly.StrongNameSigned = false;

                base.RewriteChildren(assembly);
            }

            public override List<ICustomAttribute> Rewrite(List<ICustomAttribute> customAttributes)
            {
                if (customAttributes == null)
                    return customAttributes;

                List<ICustomAttribute> newCustomAttributes = new List<ICustomAttribute>();

                // Remove all of them except for the ones that begin with Assembly
                // Also remove AssemblyFileVersion and AssemblyInformationVersion if stripFileVersionAttributes is set
                foreach (ICustomAttribute attribute in customAttributes)
                {
                    ITypeReference attributeType = attribute.Type;
                    if (attributeType is Dummy)
                        continue;

                    string typeName = TypeHelper.GetTypeName(attributeType, NameFormattingOptions.OmitContainingNamespace | NameFormattingOptions.OmitContainingType);

                    if (!typeName.StartsWith("Assembly"))
                        continue;

                    // We need to remove the signature key attribute otherwise we will not be able to re-sign these binaries.
                    if (typeName == "AssemblySignatureKeyAttribute")
                        continue;

                    if (_stripFileVersionAttributes && ((typeName == "AssemblyFileVersionAttribute" || typeName == "AssemblyInformationalVersionAttribute")))
                        continue;

                    newCustomAttributes.Add(attribute);
                }
                return base.Rewrite(newCustomAttributes);
            }
        }
    }
}