// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.Fx.ApiReviews.Differencing;
using McMaster.Extensions.CommandLineUtils;

namespace AsmDiff
{
    public class Program
    {
        [Option]
        public string OldSet { get; set; }
        [Option]
        public string NewSet { get; set; }
        
        [Option]
        public bool Unchanged { get; set; }
        [Option]
        public bool Removed { get; set; }
        [Option]
        public bool Added { get; set; }
        [Option]
        public bool Changed { get; set; }
        
        [Option]
        public bool TypesOnly { get; set; }
        [Option]
        public bool StrikeRemoved { get; set; }
        [Option]
        public bool DiffAttributes { get; set; }
        [Option]
        public bool DiffAssemblyInfo { get; set; }
        [Option]
        public bool AlwaysDiffMembers { get; set; }
        [Option]
        public bool HighlightBaseMembers { get; set; }
        
        [Option]
        public bool FlattenTypes { get; set; }
        [Option]
        public bool GroupByAssembly { get; set; }
        [Option]
        public bool ExcludeAddedTypes { get; set; }
        [Option]
        public bool ExcludeRemovedTypes { get; set; }
        [Option]
        public bool IncludeInternalApis { get; set; }
        [Option]
        public bool IncludePrivateApis { get; set; }

        [Option]
        public DiffWriterType _diffWriter { get; set; }
        [Option]
        public SyntaxWriterType SyntaxWriter { get; set; }
        
        [Option]
        public string OutFile { get; set; }

        public void OnExecute()
        {         
            if (string.IsNullOrEmpty(NewSet))
            {
                // Reset the filter to be unchanged if we only have a single set so that it will
                // simply output the contents of the set.
                Removed = Added = false;
                Unchanged = true;
            }

            if (!Added && !Removed && !Changed && !Unchanged)
            {
                // If the user didn't explicitly specify what to include we default to changes only.
                Added = Removed = Changed = true;
            }

            DiffConfigurationOptions options = GetDiffOptions();
            DiffFormat diffFormat = GetDiffFormat();
            AssemblySet oldAssemblies = AssemblySet.FromPaths(OldSet);
            AssemblySet newAssemblies = AssemblySet.FromPaths(NewSet);
            DiffConfiguration diffConfiguration = new DiffConfiguration(oldAssemblies, newAssemblies, options);
            using (TextWriter output = GetOutput())
                DiffEngine.Export(diffConfiguration, null, diffFormat, output);
        }

        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        private  DiffConfigurationOptions GetDiffOptions()
        {
            var result = DiffConfigurationOptions.IncludeAddedTypes | DiffConfigurationOptions.IncludeRemovedTypes;

            if (Added)
                result |= DiffConfigurationOptions.IncludeAdded;

            if (Removed)
                result |= DiffConfigurationOptions.IncludeRemoved;

            if (Changed)
                result |= DiffConfigurationOptions.IncludeChanged;

            if (Unchanged)
                result |= DiffConfigurationOptions.IncludeUnchanged;

            if (AlwaysDiffMembers)
                result |= DiffConfigurationOptions.AlwaysDiffMembers;

            if (TypesOnly)
                result |= DiffConfigurationOptions.TypesOnly;

            if (FlattenTypes)
                result |= DiffConfigurationOptions.FlattenTypes;

            if (GroupByAssembly)
                result |= DiffConfigurationOptions.GroupByAssembly;

            if (HighlightBaseMembers)
                result |= DiffConfigurationOptions.HighlightBaseMembers;

            if (DiffAssemblyInfo)
                result |= DiffConfigurationOptions.DiffAssemblyInfo;

            if (StrikeRemoved)
                result |= DiffConfigurationOptions.StrikeRemoved;

            if (ExcludeAddedTypes)
                result &= ~DiffConfigurationOptions.IncludeAddedTypes;

            if (ExcludeRemovedTypes)
                result &= ~DiffConfigurationOptions.IncludeRemovedTypes;

            if (IncludeInternalApis)
                result |= DiffConfigurationOptions.IncludeInternals;

            if (IncludePrivateApis)
                result |= DiffConfigurationOptions.IncludePrivates;

            return result;
        }

        private  DiffFormat GetDiffFormat()
        {
            switch (_diffWriter)
            {
                case DiffWriterType.CSharp:
                    switch (SyntaxWriter)
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

        public TextWriter GetOutput()
        {
            if (string.IsNullOrWhiteSpace(OutFile))
                return Console.Out;

            return new StreamWriter(OutFile, false, Encoding.UTF8);
        }
    }

    public enum SyntaxWriterType
    {
        Html,
        Text,
        Diff,
        Xml,
    }

    public enum DiffWriterType
    {
        CSharp,
        CSV,
    }
}
