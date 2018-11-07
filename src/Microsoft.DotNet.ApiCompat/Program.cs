// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Cci;
using Microsoft.Cci.Comparers;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Differs.Rules;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Mappings;
using Microsoft.Cci.Writers;
using Microsoft.Cci.Writers.Syntax;
using Microsoft.Fx.CommandLine;
using System.Reflection;

namespace Microsoft.DotNet.ApiCompat
{
    public class ExportCciSettings
    {
        public static IEqualityComparer<ITypeReference> StaticSettings { get; set; }
        public static IDifferenceOperands  StaticOperands { get; set; }
        public static IAttributeFilter StaticAttributeFilter { get; set; }

        public ExportCciSettings()
        {
            Settings = StaticSettings;
            Operands = StaticOperands;
            AttributeFilter = StaticAttributeFilter;
        }

        [Export(typeof(IEqualityComparer<ITypeReference>))]
        public IEqualityComparer<ITypeReference> Settings { get; }

        [Export(typeof(IDifferenceOperands))]
        public IDifferenceOperands Operands { get; }

        [Export(typeof(IAttributeFilter))]
        public IAttributeFilter AttributeFilter { get; }
    }

    public class Program
    {
        public static int Main(string[] args)
        {
            ParseCommandLine(args);
            CommandLineTraceHandler.Enable();

            if (s_listRules)
            {
                CompositionHost c = GetCompositionHost();
                ExportCciSettings.StaticSettings = CciComparers.Default.GetEqualityComparer<ITypeReference>();

                var rules = c.GetExports<IDifferenceRule>();

                foreach (var rule in rules.OrderBy(r => r.GetType().Name, StringComparer.OrdinalIgnoreCase))
                {
                    string ruleName = rule.GetType().Name;

                    if (IsOptionalRule(rule))
                        ruleName += " (optional)";

                    Console.WriteLine(ruleName);
                }

                return 0;
            }

            using (TextWriter output = GetOutput())
            {
                if (DifferenceWriter.ExitCode != 0)
                    return 0;

                if (output != Console.Out)
                    Trace.Listeners.Add(new TextWriterTraceListener(output) { Filter = new EventTypeFilter(SourceLevels.Error | SourceLevels.Warning) });
                try
                {
                    BaselineDifferenceFilter filter = GetBaselineDifferenceFilter();
                    NameTable sharedNameTable = new NameTable();
                    HostEnvironment contractHost = new HostEnvironment(sharedNameTable);
                    contractHost.UnableToResolve += new EventHandler<UnresolvedReference<IUnit, AssemblyIdentity>>(contractHost_UnableToResolve);
                    contractHost.ResolveAgainstRunningFramework = s_resolveFx;
                    contractHost.UnifyToLibPath = s_unifyToLibPaths;
                    contractHost.AddLibPaths(HostEnvironment.SplitPaths(s_contractLibDirs));
                    IEnumerable<IAssembly> contractAssemblies = contractHost.LoadAssemblies(s_contractSet, s_contractCoreAssembly);

                    if (s_ignoreDesignTimeFacades)
                        contractAssemblies = contractAssemblies.Where(a => !a.IsFacade());

                    HostEnvironment implHost = new HostEnvironment(sharedNameTable);
                    implHost.UnableToResolve += new EventHandler<UnresolvedReference<IUnit, AssemblyIdentity>>(implHost_UnableToResolve);
                    implHost.ResolveAgainstRunningFramework = s_resolveFx;
                    implHost.UnifyToLibPath = s_unifyToLibPaths;
                    implHost.AddLibPaths(HostEnvironment.SplitPaths(s_implDirs));
                    if (s_warnOnMissingAssemblies)
                        implHost.LoadErrorTreatment = ErrorTreatment.TreatAsWarning;

                    // The list of contractAssemblies already has the core assembly as the first one (if _contractCoreAssembly was specified).
                    IEnumerable<IAssembly> implAssemblies = implHost.LoadAssemblies(contractAssemblies.Select(a => a.AssemblyIdentity), s_warnOnIncorrectVersion);

                    // Exit after loading if the code is set to non-zero
                    if (DifferenceWriter.ExitCode != 0)
                        return 0;

                    ICciDifferenceWriter writer = GetDifferenceWriter(output, filter);
                    writer.Write(s_implDirs, implAssemblies, s_contractSet, contractAssemblies);
                    return 0;
                }
                catch (FileNotFoundException)
                {
                    // FileNotFoundException will be thrown by GetBaselineDifferenceFilter if it doesn't find the baseline file
                    // OR if GetComparers doesn't find the remap file.
                    return 2;
                }                
            }
        }

        private static BaselineDifferenceFilter GetBaselineDifferenceFilter()
        {
            BaselineDifferenceFilter filter = null;
            if (!string.IsNullOrEmpty(s_baselineFileName))
            {
                if (!File.Exists(s_baselineFileName))
                {
                    throw new FileNotFoundException("Baseline file {0} was not found!", s_baselineFileName);
                }
                IDifferenceFilter incompatibleFilter = new DifferenceFilter<IncompatibleDifference>();
                filter = new BaselineDifferenceFilter(incompatibleFilter, s_baselineFileName);
            }
            return filter;
        }

        private static void implHost_UnableToResolve(object sender, UnresolvedReference<IUnit, AssemblyIdentity> e)
        {
            Trace.TraceError($"Unable to resolve assembly '{e.Unresolved}' referenced by the {s_implementationOperand} assembly '{e.Referrer}'.");
        }

        private static void contractHost_UnableToResolve(object sender, UnresolvedReference<IUnit, AssemblyIdentity> e)
        {
            Trace.TraceError($"Unable to resolve assembly '{e.Unresolved}' referenced by the {s_contractOperand} assembly '{e.Referrer}'.");
        }

        private static TextWriter GetOutput()
        {
            if (string.IsNullOrWhiteSpace(s_outFile))
                return Console.Out;

            const int NumRetries = 10;
            String exceptionMessage = null;
            for (int retries = 0; retries < NumRetries; retries++)
            {
                try
                {
                    return new StreamWriter(File.OpenWrite(s_outFile));
                }
                catch (Exception e)
                {
                    exceptionMessage = e.Message;
                    System.Threading.Thread.Sleep(100);
                }
            }

            Trace.TraceError("Cannot open output file '{0}': {1}", s_outFile, exceptionMessage);
            return Console.Out;
        }

        private static ICciDifferenceWriter GetDifferenceWriter(TextWriter writer, IDifferenceFilter filter)
        {
            CompositionHost container = GetCompositionHost();

            Func<IDifferenceRuleMetadata, bool> ruleFilter =
                delegate (IDifferenceRuleMetadata ruleMetadata)
                {
                    if (ruleMetadata.OptionalRule && !s_enforceOptionalRules)
                        return false;

                    if (ruleMetadata.MdilServicingRule && !s_mdil)
                        return false;
                    return true;
                };

            if (s_mdil && s_excludeNonBrowsable)
            {
                Trace.TraceWarning("Enforcing MDIL servicing rules and exclusion of non-browsable types are both enabled, but they are not compatible so non-browsable types will not be excluded.");
            }

            MappingSettings settings = new MappingSettings();
            settings.Comparers = GetComparers();
            settings.Filter = GetCciFilter(s_mdil, s_excludeNonBrowsable);
            settings.DiffFilter = GetDiffFilter(settings.Filter);
            settings.DiffFactory = new ElementDifferenceFactory(container, ruleFilter);
            settings.GroupByAssembly = s_groupByAssembly;
            settings.IncludeForwardedTypes = true;

            if (filter == null)
            {
                filter = new DifferenceFilter<IncompatibleDifference>();
            }

            ICciDifferenceWriter diffWriter = new DifferenceWriter(writer, settings, filter);
            ExportCciSettings.StaticSettings = settings.TypeComparer;
            ExportCciSettings.StaticOperands = new DifferenceOperands()
            {
                Contract = s_contractOperand,
                Implementation = s_implementationOperand,
            };
            ExportCciSettings.StaticAttributeFilter = new AttributeFilter(s_excludeAttributesList);

            // Always compose the diff writer to allow it to import or provide exports
            container.SatisfyImports(diffWriter);

            return diffWriter;
        }

        private static CompositionHost GetCompositionHost()
        {
            var configuration = new ContainerConfiguration().WithAssembly(typeof(Program).GetTypeInfo().Assembly);
            return configuration.CreateContainer();
        }

        private static ICciComparers GetComparers()
        {
            if (!string.IsNullOrEmpty(s_remapFile))
            {
                if (!File.Exists(s_remapFile))
                {
                    throw new FileNotFoundException("ERROR: RemapFile {0} was not found!", s_remapFile);
                }
                return new NamespaceRemappingComparers(s_remapFile);
            }
            return CciComparers.Default;
        }

        private static ICciFilter GetCciFilter(bool enforcingMdilRules, bool excludeNonBrowsable)
        {
            if (enforcingMdilRules)
            {
                return new MdilPublicOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
            else if (excludeNonBrowsable)
            {
                return new PublicEditorBrowsableOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
            else
            {
                return new PublicOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
        }

        private static IMappingDifferenceFilter GetDiffFilter(ICciFilter filter)
        {
            return new MappingDifferenceFilter(GetIncludeFilter(), filter);
        }

        private static Func<DifferenceType, bool> GetIncludeFilter()
        {
            return d => d != DifferenceType.Unchanged;
        }

        private static bool IsOptionalRule(IDifferenceRule rule)
        {
            return rule.GetType().GetTypeInfo().GetCustomAttribute<ExportDifferenceRuleAttribute>().OptionalRule;
        }

        private static void ParseCommandLine(string[] args)
        {
            CommandLineParser p1 = new CommandLineParser(args);
            p1.DefineOptionalQualifier("listRules", ref s_listRules, "Outputs all the rules. If this options is supplied all other options are ignored but you must specify contracts and implDir still '/listRules \"\" /implDirs='.");

            if (s_listRules)
                return;

            CommandLineParser.ParseForConsoleApplication(delegate (CommandLineParser parser)
            {
                parser.DefineOptionalQualifier("listRules", ref s_listRules, "Outputs all the rules. If this options is supplied all other options are ignored.");
                parser.DefineAliases("baseline", "bl");
                parser.DefineOptionalQualifier("baseline", ref s_baselineFileName, "Baseline file to skip known diffs.");
                parser.DefineOptionalQualifier("remapFile", ref s_remapFile, "File with a list of type and/or namespace remappings to consider apply to names while diffing.");
                parser.DefineOptionalQualifier("groupByAssembly", ref s_groupByAssembly, "Group the differences by assembly instead of flattening the namespaces. Defaults to true.");
                parser.DefineOptionalQualifier("unifyToLibPath", ref s_unifyToLibPaths, "Unify the assembly references to the loaded assemblies and the assemblies found in the given directories (contractDepends and implDirs). Defaults to true.");
                parser.DefineOptionalQualifier("out", ref s_outFile, "Output file path. Default is the console.");
                parser.DefineOptionalQualifier("resolveFx", ref s_resolveFx, "If a contract or implementation dependency cannot be found in the given directories, fallback to try to resolve against the framework directory on the machine.");
                parser.DefineOptionalQualifier("contractDepends", ref s_contractLibDirs, "Comma delimited list of directories used to resolve the dependencies of the contract assemblies.");
                parser.DefineAliases("contractCoreAssembly", "cca");
                parser.DefineOptionalQualifier("contractCoreAssembly", ref s_contractCoreAssembly, "Simple name for the core assembly to use.");
                parser.DefineAliases("ignoreDesignTimeFacades", "idtf");
                parser.DefineOptionalQualifier("ignoreDesignTimeFacades", ref s_ignoreDesignTimeFacades, "Ignore design time facades in the contract set while analyzing.");
                parser.DefineOptionalQualifier("warnOnIncorrectVersion", ref s_warnOnIncorrectVersion, "Warn if the contract version number doesn't match the found implementation version number.");
                parser.DefineOptionalQualifier("warnOnMissingAssemblies", ref s_warnOnMissingAssemblies, "Warn if the contract assembly cannot be found in the implementation directories. Default is to error and not do anlysis.");
                parser.DefineQualifier("implDirs", ref s_implDirs, "Comma delimited list of directories to find the implementation assemblies for each contract assembly.");
                parser.DefineOptionalQualifier("mdil", ref s_mdil, "Enforce MDIL servicing rules in addition to IL rules.");
                parser.DefineAliases("excludeNonBrowsable", "enb");
                parser.DefineOptionalQualifier("excludeNonBrowsable", ref s_excludeNonBrowsable, "When MDIL servicing rules are not being enforced, exclude validation on types that are marked with EditorBrowsable(EditorBrowsableState.Never).");
                parser.DefineAliases("leftOperand", "lhs");
                parser.DefineOptionalQualifier("leftOperand", ref s_contractOperand, "Name for left operand in comparison, default is 'contract'.");
                parser.DefineAliases("rightOperand", "rhs");
                parser.DefineOptionalQualifier("rightOperand", ref s_implementationOperand, "Name for right operand in comparison, default is 'implementation'.");
                parser.DefineOptionalQualifier("excludeAttributes", ref s_excludeAttributesList, "Specify a api list in the DocId format of which attributes to exclude.");
                parser.DefineOptionalQualifier("enforceOptionalRules", ref s_enforceOptionalRules, "Enforce optional rules, in addition to the mandatory set of rules.");
                parser.DefineParameter<string>("contracts", ref s_contractSet, "Comma delimited list of assemblies or directories of assemblies for all the contract assemblies.");
            }, args);
        }

        private static string s_contractCoreAssembly;
        private static string s_contractSet;
        private static string s_implDirs;
        private static string s_contractLibDirs;
        private static bool s_listRules;
        private static string s_outFile;
        private static string s_baselineFileName;
        private static string s_remapFile;
        private static string s_contractOperand = "contract";
        private static string s_implementationOperand = "implementation";
        public static string s_excludeAttributesList;
        private static bool s_groupByAssembly = true;
        private static bool s_mdil;
        private static bool s_resolveFx;
        private static bool s_unifyToLibPaths = true;
        private static bool s_warnOnIncorrectVersion;
        private static bool s_ignoreDesignTimeFacades;
        private static bool s_excludeNonBrowsable;
        private static bool s_warnOnMissingAssemblies;
        private static bool s_enforceOptionalRules;
    }
}
