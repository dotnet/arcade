// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.Fx.ApiReviews.Differencing;
using Microsoft.Fx.CommandLine;

namespace AsmDiff
{
    internal static class Program
    {
        private static void Main()
        {
            ParseCommandLine();

            if (string.IsNullOrEmpty(_newSet))
            {
                // Reset the filter to be unchanged if we only have a single set so that it will
                // simply output the contents of the set.
                _removed = _added = false;
                _unchanged = true;
            }

            if (!_added && !_removed && !_changed && !_unchanged)
            {
                // If the user didn't explicitly specify what to include we default to changes only.
                _added = _removed = _changed = true;
            }

            DiffConfigurationOptions options = GetDiffOptions();
            DiffFormat diffFormat = GetDiffFormat();
            AssemblySet oldAssemblies = AssemblySet.FromPaths(_oldSet);
            AssemblySet newAssemblies = AssemblySet.FromPaths(_newSet);
            DiffConfiguration diffConfiguration = new DiffConfiguration(oldAssemblies, newAssemblies, options);
            using (TextWriter output = GetOutput())
                DiffEngine.Export(diffConfiguration, null, diffFormat, output);
        }

        private static DiffConfigurationOptions GetDiffOptions()
        {
            var result = DiffConfigurationOptions.IncludeAddedTypes | DiffConfigurationOptions.IncludeRemovedTypes;

            if (_added)
                result |= DiffConfigurationOptions.IncludeAdded;

            if (_removed)
                result |= DiffConfigurationOptions.IncludeRemoved;

            if (_changed)
                result |= DiffConfigurationOptions.IncludeChanged;

            if (_unchanged)
                result |= DiffConfigurationOptions.IncludeUnchanged;

            if (_alwaysDiffMembers)
                result |= DiffConfigurationOptions.AlwaysDiffMembers;

            if (_typesOnly)
                result |= DiffConfigurationOptions.TypesOnly;

            if (_flattenTypes)
                result |= DiffConfigurationOptions.FlattenTypes;

            if (_groupByAssembly)
                result |= DiffConfigurationOptions.GroupByAssembly;

            if (_highlightBaseMembers)
                result |= DiffConfigurationOptions.HighlightBaseMembers;

            if (_diffAssemblyInfo)
                result |= DiffConfigurationOptions.DiffAssemblyInfo;

            if (_strikeRemoved)
                result |= DiffConfigurationOptions.StrikeRemoved;

            if (_excludeAddedTypes)
                result &= ~DiffConfigurationOptions.IncludeAddedTypes;

            if (_excludeRemovedTypes)
                result &= ~DiffConfigurationOptions.IncludeRemovedTypes;

            if (_includeInternalApis)
                result |= DiffConfigurationOptions.IncludeInternals;

            if (_includePrivateApis)
                result |= DiffConfigurationOptions.IncludePrivates;

            return result;
        }

        private static DiffFormat GetDiffFormat()
        {
            switch (_diffWriter)
            {
                case DiffWriterType.CSharp:
                    switch (_syntaxWriter)
                    {
                        case SyntaxWriterType.Html:
                            return DiffFormat.Html;
                        case SyntaxWriterType.Text:
                            return DiffFormat.Text;
                        case SyntaxWriterType.Diff:
                            return DiffFormat.UnifiedDiff;
                        case SyntaxWriterType.Xml:
                            return DiffFormat.WordXml;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                case DiffWriterType.CSV:
                    return DiffFormat.Csv;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static TextWriter GetOutput()
        {
            if (string.IsNullOrWhiteSpace(_outFile))
                return Console.Out;

            return new StreamWriter(_outFile, false, Encoding.UTF8);
        }

        private static void ParseCommandLine()
        {
            CommandLineParser.ParseForConsoleApplication(delegate(CommandLineParser parser)
            {
                parser.DefineAliases("unchanged", "u");
                parser.DefineOptionalQualifier("unchanged", ref _unchanged, "(-u) Include members, types, and namespaces that are unchanged.");
                
                parser.DefineAliases("removed", "r");
                parser.DefineOptionalQualifier("removed", ref _removed, "(-r) Include members, types, and namespaces that were removed. (default is removed and added)");
                
                parser.DefineAliases("added", "a");
                parser.DefineOptionalQualifier("added", ref _added, "(-a) Include members, types, and namespaces that where added. (default is removed and added)");
                
                parser.DefineAliases("changed", "c");
                parser.DefineOptionalQualifier("changed", ref _changed, "(-c) Include members, types, and namespaces that have changed.");
                
                parser.DefineAliases("syntax", "s");
                parser.DefineOptionalQualifier<SyntaxWriterType>("syntax", ref _syntaxWriter, "(-s) Specific the syntax writer type. Only used if the writer is CSDecl");
                
                parser.DefineAliases("strikeRemoved", "sr");
                parser.DefineOptionalQualifier<bool>("strikeRemoved", ref _strikeRemoved, "(-sr) For removed API's also strike them out. This option currently only works with the HTML writer which is the default."); 
                
                parser.DefineAliases("diffAttributes", "da");
                parser.DefineOptionalQualifier("diffAttributes", ref _diffAttributes, "(-da) Enables diffing of the attributes as well, by default all attributes are ignored. For CSV writer causes the assembly name to be included in the column for types.");
                
                parser.DefineAliases("diffAssemblyInfo", "dai");
                parser.DefineOptionalQualifier("diffAssemblyInfo", ref _diffAssemblyInfo, "(-dai) Enables diffing of the assembly level information like version, key, etc.");
                
                parser.DefineAliases("alwaysDiffMembers", "adm");
                parser.DefineOptionalQualifier("alwaysDiffMembers", ref _alwaysDiffMembers, "(-adm) By default if an entire class is added or removed we don't show the members, setting this option forces all the members to be shown instead.");

                parser.DefineAliases("highlightBaseMembers", "hbm");
                parser.DefineOptionalQualifier("highlightBaseMembers", ref _highlightBaseMembers, "(-hbm) Highlight members that are interface implementations or overrides of a base member.");
                
                parser.DefineAliases("groupByAssembly", "gba");
                parser.DefineOptionalQualifier("groupByAssembly", ref _groupByAssembly, "(-gba) Group the differences by assembly instead of flattening the namespaces.");
                
                parser.DefineAliases("flattenTypes", "ft");
                parser.DefineOptionalQualifier("flattenTypes", ref _flattenTypes, "(-ft) Will flatten types so that all members available on the type show on the type not just the implemented ones.");
                
                parser.DefineAliases("typesonly", "to");
                parser.DefineOptionalQualifier("typesOnly", ref _typesOnly, "(-to) Only show down to the type level not the member level.");
                
                parser.DefineAliases("diffWriter", "w");
                parser.DefineOptionalQualifier("diffWriter", ref _diffWriter, "(-w) Type of difference writer to use, either CSharp code diffs or flat list of compat violations (default).");
                
                parser.DefineAliases("excludeAddedTypes", "eat");
                parser.DefineOptionalQualifier("excludeAddedTypes", ref _excludeAddedTypes, "(-eat) Do not show types that have been added to the new set of types.");

                parser.DefineAliases("excludeRemovedTypes", "ert");
                parser.DefineOptionalQualifier("excludeRemovedTypes", ref _excludeRemovedTypes, "(-ert) Do not show types that have been removed from the new set of types.");

                parser.DefineAliases("includeInternalApis", "iia");
                parser.DefineOptionalQualifier("includeInternalApis", ref _includeInternalApis, "(-iia) Include internal types and members as part of the diff.");

                parser.DefineAliases("includePrivateApis", "ipa");
                parser.DefineOptionalQualifier("includePrivateApis", ref _includePrivateApis, "(-ipa) Include private types and members as part of the diff.");

                parser.DefineOptionalQualifier("out", ref _outFile, "Output file path. Default is the console.");
                
                parser.DefineParameter<string>("oldset", ref _oldSet, "Provide path to an assembly or directory for an assembly set to gather the old set of types. These types will be the baseline for the compare.");
                
                parser.DefineOptionalParameter<string>("newset", ref _newSet, "Provide path to an assembly or directory for an assembly set to gather the new set of types. If this parameter is not provided the API's for the oldset will be printed instead of the diff.");
            });
        }

        private static string _oldSet;
        private static string _newSet;
        private static bool _unchanged;
        private static bool _removed;
        private static bool _added;
        private static bool _changed;
        private static bool _typesOnly;
        private static SyntaxWriterType _syntaxWriter;
        private static string _outFile;
        private static bool _strikeRemoved;
        private static bool _diffAttributes;
        private static bool _diffAssemblyInfo;
        private static bool _alwaysDiffMembers;
        private static bool _highlightBaseMembers;
        private static bool _flattenTypes;
        private static bool _groupByAssembly;
        private static bool _excludeAddedTypes;
        private static bool _excludeRemovedTypes;
        private static bool _includeInternalApis;
        private static bool _includePrivateApis;

        private static DiffWriterType _diffWriter;

        private enum SyntaxWriterType
        {
            Html,
            Text,
            Diff,
            Xml,
        }

        private enum DiffWriterType
        {
            CSharp,
            CSV,
        }
    }
}
