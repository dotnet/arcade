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
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;

namespace Microsoft.DotNet.ApiCompat
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "ApiCompat",
                FullName = "A command line tool to verify that two sets of APIs are compatible.",
                ResponseFileHandling = ResponseFileHandling.ParseArgsAsSpaceSeparated
            };
            app.HelpOption("-?|-h|--help");
            app.VersionOption("-v|--version", GetAssemblyVersion());

            CommandArgument contracts = app.Argument("contracts", "Comma delimited list of assemblies or directories of assemblies for all the contract assemblies.");
            contracts.IsRequired();
            CommandOption implDirs = app.Option("-i|--impl-dirs", "Comma delimited list of directories to find the implementation assemblies for each contract assembly.", CommandOptionType.SingleValue);
            implDirs.IsRequired(allowEmptyStrings: true);
            CommandOption baseline = app.Option("-b|--baseline", "Baseline file to skip known diffs.", CommandOptionType.SingleValue);
            CommandOption mdil = app.Option("-m|--mdil", "Enforce MDIL servicing rules in addition to IL rules.", CommandOptionType.NoValue);
            CommandOption outFilePath = app.Option("-o|--out", "Output file path. Default is the console.", CommandOptionType.SingleValue);
            CommandOption leftOperand = app.Option("-l|--left-operand", "Name for left operand in comparison, default is 'contract'.", CommandOptionType.SingleValue);
            CommandOption rightOperand = app.Option("-r|--right-operand", "Name for right operand in comparison, default is 'implementation'.", CommandOptionType.SingleValue);
            CommandOption listRules = app.Option("--list-rules", "Outputs all the rules. If this options is supplied all other options are ignored.", CommandOptionType.NoValue);
            CommandOption remapFile = app.Option("--remap-file", "File with a list of type and/or namespace remappings to consider apply to names while diffing.", CommandOptionType.SingleValue);
            CommandOption skipGroupByAssembly = app.Option("--skip-group-by-assembly", "Skip grouping the differences by assembly instead of flattening the namespaces.", CommandOptionType.NoValue);
            CommandOption skipUnifyToLibPath = app.Option("--skip-unify-to-lib-path", "Skip unifying the assembly references to the loaded assemblies and the assemblies found in the given directories (contractDepends and implDirs).", CommandOptionType.NoValue);
            CommandOption resolveFx = app.Option("--resolve-fx", "If a contract or implementation dependency cannot be found in the given directories, fallback to try to resolve against the framework directory on the machine.", CommandOptionType.NoValue);
            CommandOption contractDepends = app.Option("--contract-depends", "Comma delimited list of directories used to resolve the dependencies of the contract assemblies.", CommandOptionType.SingleValue);
            CommandOption contractCoreAssembly = app.Option("--contract-core-assembly", "Simple name for the core assembly to use.", CommandOptionType.SingleValue);
            CommandOption ignoreDesignTimeFacades = app.Option("--ignore-design-time-facades", "Ignore design time facades in the contract set while analyzing.", CommandOptionType.NoValue);
            CommandOption warnOnIncorrectVersion = app.Option("--warn-on-incorrect-version", "Warn if the contract version number doesn't match the found implementation version number.", CommandOptionType.NoValue);
            CommandOption warnOnMissingAssemblies = app.Option("--warn-on-missing-assemblies", "Warn if the contract assembly cannot be found in the implementation directories. Default is to error and not do analysis.", CommandOptionType.NoValue);
            CommandOption excludeNonBrowsable = app.Option("--exclude-non-browsable", "When MDIL servicing rules are not being enforced, exclude validation on types that are marked with EditorBrowsable(EditorBrowsableState.Never).", CommandOptionType.NoValue);
            CommandOption excludeAttributes = app.Option("--exclude-attributes", "Specify a api list in the DocId format of which attributes to exclude.", CommandOptionType.SingleValue);
            CommandOption enforceOptionalRules = app.Option("--enforce-optional-rules", "Enforce optional rules, in addition to the mandatory set of rules.", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                string leftOperandValue = leftOperand.HasValue() ? leftOperand.Value() : "contract";
                string rightOperandValue = rightOperand.HasValue() ? rightOperand.Value() : "implementation";

                if (listRules.HasValue())
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

                using (TextWriter output = GetOutput(outFilePath.Value()))
                {
                    if (DifferenceWriter.ExitCode != 0)
                        return 0;

                    if (output != Console.Out)
                        Trace.Listeners.Add(new TextWriterTraceListener(output) { Filter = new EventTypeFilter(SourceLevels.Error | SourceLevels.Warning) });

                    try
                    {
                        BaselineDifferenceFilter filter = GetBaselineDifferenceFilter(baseline.Value());
                        NameTable sharedNameTable = new NameTable();
                        HostEnvironment contractHost = new HostEnvironment(sharedNameTable);
                        contractHost.UnableToResolve += (sender, e) => Trace.TraceError($"Unable to resolve assembly '{e.Unresolved}' referenced by the {leftOperandValue} assembly '{e.Referrer}'.");
                        contractHost.ResolveAgainstRunningFramework = resolveFx.HasValue();
                        contractHost.UnifyToLibPath = !skipUnifyToLibPath.HasValue();
                        contractHost.AddLibPaths(HostEnvironment.SplitPaths(contractDepends.Value()));
                        IEnumerable<IAssembly> contractAssemblies = contractHost.LoadAssemblies(contracts.Value, contractCoreAssembly.Value());

                        if (ignoreDesignTimeFacades.HasValue())
                            contractAssemblies = contractAssemblies.Where(a => !a.IsFacade());

                        HostEnvironment implHost = new HostEnvironment(sharedNameTable);
                        implHost.UnableToResolve += (sender, e) => Trace.TraceError($"Unable to resolve assembly '{e.Unresolved}' referenced by the {rightOperandValue} assembly '{e.Referrer}'.");
                        implHost.ResolveAgainstRunningFramework = resolveFx.HasValue();
                        implHost.UnifyToLibPath = !skipUnifyToLibPath.HasValue();
                        implHost.AddLibPaths(HostEnvironment.SplitPaths(implDirs.Value()));
                        if (warnOnMissingAssemblies.HasValue())
                            implHost.LoadErrorTreatment = ErrorTreatment.TreatAsWarning;

                        // The list of contractAssemblies already has the core assembly as the first one (if _contractCoreAssembly was specified).
                        IEnumerable<IAssembly> implAssemblies = implHost.LoadAssemblies(contractAssemblies.Select(a => a.AssemblyIdentity), warnOnIncorrectVersion.HasValue());

                        // Exit after loading if the code is set to non-zero
                        if (DifferenceWriter.ExitCode != 0)
                            return 0;

                        ICciDifferenceWriter writer = GetDifferenceWriter(output, filter, enforceOptionalRules.HasValue(), mdil.HasValue(), 
                            excludeNonBrowsable.HasValue(), remapFile.Value(), !skipGroupByAssembly.HasValue(), leftOperandValue, rightOperandValue, excludeAttributes.Value());
                        writer.Write(implDirs.Value(), implAssemblies, contracts.Value, contractAssemblies);

                        return 0;
                    }
                    catch (FileNotFoundException)
                    {
                        // FileNotFoundException will be thrown by GetBaselineDifferenceFilter if it doesn't find the baseline file
                        // OR if GetComparers doesn't find the remap file.
                        return 2;
                    }
                }
            });
            
            return app.Execute(args);            
        }

        private static ICciDifferenceWriter GetDifferenceWriter(TextWriter writer, 
            IDifferenceFilter filter,
            bool enforceOptionalRules,
            bool mdil,
            bool excludeNonBrowsable,
            string remapFile,
            bool groupByAssembly,
            string leftOperand,
            string rightOperand,
            string excludeAttributes)
        {
            CompositionHost container = GetCompositionHost();

            bool RuleFilter(IDifferenceRuleMetadata ruleMetadata)
            {
                if (ruleMetadata.OptionalRule && !enforceOptionalRules)
                    return false;

                if (ruleMetadata.MdilServicingRule && !mdil)
                    return false;
                return true;
            }

            if (mdil && excludeNonBrowsable)
            {
                Trace.TraceWarning("Enforcing MDIL servicing rules and exclusion of non-browsable types are both enabled, but they are not compatible so non-browsable types will not be excluded.");
            }

            var settings = new MappingSettings
            {
                Comparers = GetComparers(remapFile),
                Filter = GetCciFilter(mdil, excludeNonBrowsable)
            };
            settings.DiffFilter = GetDiffFilter(settings.Filter);
            settings.DiffFactory = new ElementDifferenceFactory(container, RuleFilter);
            settings.GroupByAssembly = groupByAssembly;
            settings.IncludeForwardedTypes = true;

            if (filter == null)
            {
                filter = new DifferenceFilter<IncompatibleDifference>();
            }

            var diffWriter = new DifferenceWriter(writer, settings, filter);
            ExportCciSettings.StaticSettings = settings.TypeComparer;
            ExportCciSettings.StaticOperands = new DifferenceOperands()
            {
                Contract = leftOperand,
                Implementation = rightOperand
            };
            ExportCciSettings.StaticAttributeFilter = new AttributeFilter(excludeAttributes);

            // Always compose the diff writer to allow it to import or provide exports
            container.SatisfyImports(diffWriter);

            return diffWriter;
        }

        private static BaselineDifferenceFilter GetBaselineDifferenceFilter(string baselineFileName)
        {
            BaselineDifferenceFilter filter = null;
            if (!string.IsNullOrEmpty(baselineFileName))
            {
                if (!File.Exists(baselineFileName))
                {
                    throw new FileNotFoundException("Baseline file {0} was not found!", baselineFileName);
                }
                IDifferenceFilter incompatibleFilter = new DifferenceFilter<IncompatibleDifference>();
                filter = new BaselineDifferenceFilter(incompatibleFilter, baselineFileName);
            }
            return filter;
        }

        private static TextWriter GetOutput(string outFilePath)
        {
            if (string.IsNullOrWhiteSpace(outFilePath))
                return Console.Out;

            const int NumRetries = 10;
            string exceptionMessage = null;
            for (int retries = 0; retries < NumRetries; retries++)
            {
                try
                {
                    return new StreamWriter(File.OpenWrite(outFilePath));
                }
                catch (Exception e)
                {
                    exceptionMessage = e.Message;
                    System.Threading.Thread.Sleep(100);
                }
            }

            Trace.TraceError("Cannot open output file '{0}': {1}", outFilePath, exceptionMessage);
            return Console.Out;
        }

        private static CompositionHost GetCompositionHost()
        {
            var configuration = new ContainerConfiguration().WithAssembly(typeof(Program).GetTypeInfo().Assembly);
            return configuration.CreateContainer();
        }

        private static ICciComparers GetComparers(string remapFile)
        {
            if (!string.IsNullOrEmpty(remapFile))
            {
                if (!File.Exists(remapFile))
                {
                    throw new FileNotFoundException("ERROR: RemapFile {0} was not found!", remapFile);
                }
                return new NamespaceRemappingComparers(remapFile);
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

        private static IMappingDifferenceFilter GetDiffFilter(ICciFilter filter) =>
            new MappingDifferenceFilter(GetIncludeFilter(), filter);

        private static Func<DifferenceType, bool> GetIncludeFilter() => (d => d != DifferenceType.Unchanged);

        private static bool IsOptionalRule(IDifferenceRule rule) =>
            rule.GetType().GetTypeInfo().GetCustomAttribute<ExportDifferenceRuleAttribute>().OptionalRule;

        private static string GetAssemblyVersion() => typeof(Program).Assembly.GetName().Version.ToString();
    }
}
