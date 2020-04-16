// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Mappings;
using Microsoft.Cci.Writers;
using Microsoft.Cci.Writers.Syntax;
using Microsoft.DotNet.AsmDiff.CSV;

namespace Microsoft.DotNet.AsmDiff
{
    public static class DiffEngine
    {
        public static void Export(DiffConfiguration configuration, IEnumerable<DiffComment> diffComments, DiffFormat format, TextWriter streamWriter)
        {
            var strikeOutRemoved = configuration.IsOptionSet(DiffConfigurationOptions.StrikeRemoved);
            using (var syntaxWriter = GetExportWriter(format, streamWriter, strikeOutRemoved))
            {
                var writer = CreateExportWriter(format, streamWriter, configuration, syntaxWriter, diffComments);
                WriteDiff(configuration, writer);
            }
        }

        private static void WriteDiff(DiffConfiguration configuration, ICciDifferenceWriter writer)
        {
            var oldSet = configuration.Left;
            var oldAssemblies = oldSet.Assemblies;
            var oldAssembliesName = oldSet.Name;

            // The diff writer special cases the name being null
            // to indicated that there is only one "set".
            var newSet = configuration.Right;
            var newAssembliesName = newSet.IsNull
                                        ? null
                                        : newSet.Name;
            var newAssemblies = newSet.Assemblies;

            writer.Write(oldAssembliesName, oldAssemblies, newAssembliesName, newAssemblies);
        }

        private static ICciDifferenceWriter CreateExportWriter(DiffFormat format, TextWriter textWriter, DiffConfiguration configuration, IStyleSyntaxWriter writer, IEnumerable<DiffComment> diffComments)
        {
            var mappingSettings = GetMappingSettings(configuration);
            var includeAttributes = configuration.IsOptionSet(DiffConfigurationOptions.DiffAttributes);

            switch (format)
            {
                case DiffFormat.Csv:
                    var diffColumns = DiffCsvColumn.CreateStandardColumns(configuration).Where(c => c.IsVisible).ToArray();
                    var csvTextWriter = new CsvTextWriter(textWriter);
                    csvTextWriter.WriteLine(diffColumns.Select(c => c.Name));
                    return new DiffCsvWriter(csvTextWriter, mappingSettings, diffColumns, CancellationToken.None);
                case DiffFormat.Html:
                    return new DiffCSharpWriter(writer, mappingSettings, diffComments, includeAttributes)
                    {
                        IncludeAssemblyProperties = configuration.IsOptionSet(DiffConfigurationOptions.DiffAssemblyInfo),
                        HighlightBaseMembers = configuration.IsOptionSet(DiffConfigurationOptions.HighlightBaseMembers)
                    };
                case DiffFormat.WordXml:
                case DiffFormat.Text:
                case DiffFormat.UnifiedDiff:
                    return new DiffCSharpWriter(writer, mappingSettings, diffComments, includeAttributes)
                               {
                                   IncludeAssemblyProperties = configuration.IsOptionSet(DiffConfigurationOptions.DiffAssemblyInfo),
                                   HighlightBaseMembers = configuration.IsOptionSet(DiffConfigurationOptions.HighlightBaseMembers)
                               };
                default:
                    throw new ArgumentOutOfRangeException("format");
            }
        }

        private static IStyleSyntaxWriter GetExportWriter(DiffFormat format, TextWriter textWriter, bool strikeOutRemoved)
        {
            switch (format)
            {
                case DiffFormat.Csv:
                    return null;
                case DiffFormat.Html:
                    return new HtmlSyntaxWriter(textWriter) {StrikeOutRemoved = strikeOutRemoved};
                case DiffFormat.WordXml:
                    return new OpenXmlSyntaxWriter(textWriter);
                case DiffFormat.Text:
                    return new TextSyntaxWriter(textWriter);
                case DiffFormat.UnifiedDiff:
                    return new UnifiedDiffSyntaxWriter(textWriter);
                default:
                    throw new ArgumentOutOfRangeException("format");
            }
        }

        public static MappingSettings GetMappingSettings(DiffConfiguration configuration)
        {
            Func<DifferenceType, bool> diffFilterPredicate = t => (t != DifferenceType.Added || configuration.IsOptionSet(DiffConfigurationOptions.IncludeAdded)) &&
                                                                  (t != DifferenceType.Changed || configuration.IsOptionSet(DiffConfigurationOptions.IncludeChanged)) &&
                                                                  (t != DifferenceType.Removed || configuration.IsOptionSet(DiffConfigurationOptions.IncludeRemoved)) &&
                                                                  (t != DifferenceType.Unchanged || configuration.IsOptionSet(DiffConfigurationOptions.IncludeUnchanged));

            var cciFilter = GetFilter(configuration);
            var mappingDifferenceFilter = configuration.IsOptionSet(DiffConfigurationOptions.TypesOnly)
                                              ? new TypesOnlyMappingDifferenceFilter(diffFilterPredicate, cciFilter)
                                              : new MappingDifferenceFilter(diffFilterPredicate, cciFilter);

            var includeAddedTypes = configuration.IsOptionSet(DiffConfigurationOptions.IncludeAddedTypes);
            var includeRemovedTypes = configuration.IsOptionSet(DiffConfigurationOptions.IncludeRemovedTypes);
            var actualFilter = includeAddedTypes && includeRemovedTypes
                                   ? (IMappingDifferenceFilter) mappingDifferenceFilter
                                   : new CommonTypesMappingDifferenceFilter(mappingDifferenceFilter, includeAddedTypes, includeRemovedTypes);

            return new MappingSettings
                       {
                           Filter = cciFilter,
                           DiffFilter = actualFilter,
                           IncludeForwardedTypes = true,
                           GroupByAssembly = configuration.IsOptionSet(DiffConfigurationOptions.GroupByAssembly),
                           FlattenTypeMembers = configuration.IsOptionSet(DiffConfigurationOptions.FlattenTypes),
                           AlwaysDiffMembers = configuration.IsOptionSet(DiffConfigurationOptions.AlwaysDiffMembers)
                       };
        }

        private static ICciFilter GetFilter(DiffConfiguration configuration)
        {
            var includeAttributes = configuration.IsOptionSet(DiffConfigurationOptions.DiffAttributes);
            var includeInternals = configuration.IsOptionSet(DiffConfigurationOptions.IncludeInternals);
            var includePrivates = configuration.IsOptionSet(DiffConfigurationOptions.IncludePrivates);
            var includeGenerated = configuration.IsOptionSet(DiffConfigurationOptions.IncludeGenerated);
            return new DiffCciFilter(includeAttributes, includeInternals, includePrivates, includeGenerated);
        }
    }
}
