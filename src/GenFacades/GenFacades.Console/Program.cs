using Microsoft.Cci.Extensions;
using Microsoft.Fx.CommandLine;
using System;

namespace GenFacades
{
    public class Program
    {
        public static int Main(string[] args)
        {
            string seeds = null;
            string contracts = null;
            string facadePath = null;
            Version assemblyFileVersion = null;
            bool clearBuildAndRevision = false;
            bool ignoreMissingTypes = false;
            bool buildPartialReferenceFacade = false;
            bool ignoreBuildAndRevisionMismatch = false;
            bool buildDesignTimeFacades = false;
            string inclusionContracts = null;
            ErrorTreatment seedLoadErrorTreatment = ErrorTreatment.Default;
            ErrorTreatment contractLoadErrorTreatment = ErrorTreatment.Default;
            string[] seedTypePreferencesUnsplit = null;
            bool forceZeroVersionSeeds = false;
            bool producePdb = true;
            string partialFacadeAssemblyPath = null;

            bool parsingSucceeded = CommandLineParser.ParseForConsoleApplication((parser) =>
            {
                parser.DefineQualifier("facadePath", ref facadePath, "Path to output the facades.");
                parser.DefineQualifier("seeds", ref seeds, "Path to the seed assemblies. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineQualifier("contracts", ref contracts, "Path to the contract assemblies. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineOptionalQualifier("assemblyFileVersion", ref assemblyFileVersion, "Override the AssemblyFileVersion attribute from the contract with the given version for the generated facade.");
                parser.DefineOptionalQualifier("clearBuildAndRevision", ref clearBuildAndRevision, "Generate facade assembly version x.y.0.0 for contract version x.y.z.w");
                parser.DefineOptionalQualifier("ignoreBuildAndRevisionMismatch", ref ignoreBuildAndRevisionMismatch, "Ignore a mismatch in revision and build for partial facade.");
                parser.DefineOptionalQualifier("ignoreMissingTypes", ref ignoreMissingTypes, "Ignore types that cannot be found in the seed assemblies. This is not recommended but is sometimes helpful while hacking around or trying to produce partial facades.");
                parser.DefineOptionalQualifier("buildPartialReferenceFacade", ref buildPartialReferenceFacade, "Preserves all metadata from the contract and replaces types that can be found in the seeds with type forwards.");
                parser.DefineOptionalQualifier("designTime", ref buildDesignTimeFacades, "Enable design-time facade generation (marks facades with reference assembly flag and attribute).");
                parser.DefineOptionalQualifier("include", ref inclusionContracts, "Add types from these contracts to the facades. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineOptionalQualifier("seedError", ref seedLoadErrorTreatment, "Error handling for seed assembly load failure.");
                parser.DefineOptionalQualifier("contractError", ref seedLoadErrorTreatment, "Error handling for contract assembly load failure.");
                parser.DefineOptionalQualifier("preferSeedType", ref seedTypePreferencesUnsplit, "Set which seed assembly to choose for a given type when it is defined in more than one assembly. Format: FullTypeName=PreferredSeedAssemblyName");
                parser.DefineOptionalQualifier("forceZeroVersionSeeds", ref forceZeroVersionSeeds, "Forces all seed assembly versions to 0.0.0.0, regardless of their true version.");
                parser.DefineOptionalQualifier("partialFacadeAssemblyPath", ref partialFacadeAssemblyPath, "Specifies the path to a single partial facade assembly, into which appropriate type forwards will be added to satisfy the given contract. If this option is specified, only a single partial assembly and a single contract may be given.");
                parser.DefineOptionalQualifier("producePdb", ref producePdb, "Specifices if a PDB file should be produced for the resulting partial facade.");
            }, args);

            if (!parsingSucceeded)
            {
                return 1;
            }

            CommandLineTraceHandler.Enable();

            return Generator.Execute(
                seeds,
                contracts,
                facadePath,
                assemblyFileVersion,
                clearBuildAndRevision,
                ignoreMissingTypes,
                ignoreBuildAndRevisionMismatch,
                buildDesignTimeFacades,
                inclusionContracts,
                seedLoadErrorTreatment,
                contractLoadErrorTreatment,
                seedTypePreferencesUnsplit,
                forceZeroVersionSeeds,
                producePdb,
                partialFacadeAssemblyPath,
                buildPartialReferenceFacade) ? 0 : 1;
        }
    }
}