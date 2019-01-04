// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Writers;
using Microsoft.Cci.Writers.Syntax;
using McMaster.Extensions.CommandLineUtils;

namespace Microsoft.DotNet.GenAPI
{
    internal partial class Program
    {
        private static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "ApiCompat",
                FullName = "A command line tool to verify that two sets of APIs are compatible.",
                ResponseFileHandling = ResponseFileHandling.ParseArgsAsSpaceSeparated
            };
            app.HelpOption("-?|-h|--help");
            app.VersionOption("-v|--version", GetAssemblyVersion());

            CommandArgument assemblyArg = app.Argument("assembly", "Path for an specific assembly or a directory to get all assemblies.");
            assemblyArg.IsRequired();
            CommandOption libPath = app.Option("-l|--lib-path", "Delimited (',' or ';') set of paths to use for resolving assembly references", CommandOptionType.SingleValue);
            CommandOption apiList = app.Option("-a|--api-list", "Specify a api list in the DocId format of which APIs to include.", CommandOptionType.SingleValue);
            CommandOption outFilePath = app.Option("-o|--out", "Output path. Default is the console. Can specify an existing directory as well and then a file will be created for each assembly with the matching name of the assembly.", CommandOptionType.SingleValue);
            CommandOption headerFile = app.Option("-h|--header-file", "Specify a file with header content to prepend to output.", CommandOptionType.SingleValue);
            CommandOption<WriterType> writerType = app.Option<WriterType>("-w|--writer", "Specify the writer type to use. Legal values: CSDecl, DocIds, TypeForwards, TypeList. Default is CSDecl.", CommandOptionType.SingleValue);
            CommandOption<SyntaxWriterType> syntaxWriterType = app.Option<SyntaxWriterType>("-s|--syntax", "Specific the syntax writer type. Only used if the writer is CSDecl. Legal values: Text, Html, Xml. Default is Text.", CommandOptionType.SingleValue);
            CommandOption<DocIdKinds> docIdKinds = app.Option<DocIdKinds>("-d|--doc-id-kinds", "Only include API of the specified kinds. Legal values: A, Assembly, Namespace, N, T, Type, Field, F, P, Property, Method, M, Event, E, All. Default is All.", CommandOptionType.SingleValue);
            CommandOption exceptionMessage = app.Option("-t|--throw", "Method bodies should throw PlatformNotSupportedException.", CommandOptionType.SingleValue);
            CommandOption globalPrefix = app.Option("-g|--global", "Include global prefix for compilation.", CommandOptionType.NoValue);
            CommandOption excludeApiList = app.Option("--exclude-api-list", "Specify a api list in the DocId format of which APIs to exclude.", CommandOptionType.SingleValue);
            CommandOption excludeAttributesList = app.Option("--exclude-attributes-list", "Specify a list in the DocId format of which attributes should be excluded from being applied on apis.", CommandOptionType.SingleValue);
            CommandOption apiOnly = app.Option("--api-only", "[CSDecl] Include only API's not CS code that compiles.", CommandOptionType.NoValue);
            CommandOption all = app.Option("--all", "Include all API's not just public APIs. Default is public only.", CommandOptionType.NoValue);
            CommandOption memberHeadings = app.Option("--member-headings", "[CSDecl] Include member headings for each type of member.", CommandOptionType.NoValue);
            CommandOption hightlightBaseMembers = app.Option("--hightlight-base-members", "[CSDecl] Highlight overridden base members.", CommandOptionType.NoValue);
            CommandOption hightlightInterfaceMembers = app.Option("--hightlight-interface-members", "[CSDecl] Highlight interface implementation members.", CommandOptionType.NoValue);
            CommandOption alwaysIncludeBase = app.Option("--always-include-base", "[CSDecl] Include base types, interfaces, and attributes, even when those types are filtered.", CommandOptionType.NoValue);
            CommandOption excludeMembers = app.Option("--exclude-members", "Exclude members when return value or parameter types are excluded.", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                HostEnvironment host = new HostEnvironment();
                host.UnableToResolve += (sender, e) => 
                  Console.WriteLine("Unable to resolve assembly '{0}' referenced by '{1}'.", e.Unresolved.ToString(), e.Referrer.ToString()); ;

                host.UnifyToLibPath = true;
                if (!string.IsNullOrWhiteSpace(libPath.Value()))
                    host.AddLibPaths(HostEnvironment.SplitPaths(libPath.Value()));

                IEnumerable<IAssembly> assemblies = host.LoadAssemblies(HostEnvironment.SplitPaths(assemblyArg.Value));

                if (!assemblies.Any())
                {
                    Console.WriteLine("ERROR: Failed to load any assemblies from '{0}'", assemblyArg.Value);
                    return 1;
                }

                string headerText = GetHeaderText(headerFile.Value());
                bool loopPerAssembly = Directory.Exists(outFilePath.Value());

                if (loopPerAssembly)
                {
                    foreach (var assembly in assemblies)
                    {
                        using (TextWriter output = GetOutput(GetFilename(assembly, writerType.ParsedValue, syntaxWriterType.ParsedValue)))
                        using (IStyleSyntaxWriter syntaxWriter = GetSyntaxWriter(output, writerType.ParsedValue, syntaxWriterType.ParsedValue))
                        {
                            if (headerText != null)
                                output.Write(headerText);
                            ICciWriter writer = GetWriter(output, syntaxWriter);
                            writer.WriteAssemblies(new IAssembly[] { assembly });
                        }
                    }
                }
                else
                {
                    using (TextWriter output = GetOutput(outFilePath.Value()))
                    using (IStyleSyntaxWriter syntaxWriter = GetSyntaxWriter(output, writerType.ParsedValue, syntaxWriterType.ParsedValue))
                    {
                        if (headerText != null)
                            output.Write(headerText);
                        ICciWriter writer = GetWriter(output, syntaxWriter);
                        writer.WriteAssemblies(assemblies);
                    }
                }

                return 0;
            });

            ICciWriter GetWriter(TextWriter output, ISyntaxWriter syntaxWriter)
            {
                ICciFilter filter = GetFilter(apiList.Value(), all.HasValue(), apiOnly.HasValue(), 
                    excludeApiList.Value(), excludeMembers.HasValue(), excludeAttributesList.Value());

                switch (writerType.ParsedValue)
                {
                    case WriterType.DocIds:
                        DocIdKinds docIdKind = docIdKinds.HasValue() ? docIdKinds.ParsedValue : DocIdKinds.All;
                        return new DocumentIdWriter(output, filter, docIdKind);
                    case WriterType.TypeForwards:
                        return new TypeForwardWriter(output, filter)
                        {
                            IncludeForwardedTypes = true
                        };
                    case WriterType.TypeList:
                        return new TypeListWriter(syntaxWriter, filter);
                    default:
                    case WriterType.CSDecl:
                        {
                            CSharpWriter writer = new CSharpWriter(syntaxWriter, filter, apiOnly.HasValue());
                            writer.IncludeSpaceBetweenMemberGroups = writer.IncludeMemberGroupHeadings = memberHeadings.HasValue();
                            writer.HighlightBaseMembers = hightlightBaseMembers.HasValue();
                            writer.HighlightInterfaceMembers = hightlightInterfaceMembers.HasValue();
                            writer.PutBraceOnNewLine = true;
                            writer.PlatformNotSupportedExceptionMessage = exceptionMessage.Value();
                            writer.IncludeGlobalPrefixForCompilation = globalPrefix.HasValue();
                            writer.AlwaysIncludeBase = alwaysIncludeBase.HasValue();
                            return writer;
                        }
                }
            }

            return app.Execute(args);
        }

        private static string GetHeaderText(string headerFile)
        {
            if (string.IsNullOrEmpty(headerFile))
            {
                return null;
            }

            if (!File.Exists(headerFile))
            {
                Console.WriteLine("ERROR: header file '{0}' does not exist", headerFile);
                return null;
            }

            return File.ReadAllText(headerFile);
        }

        private static TextWriter GetOutput(string outFilePath, string filename = "")
        {
            // If this is a null, empty, whitespace, or a directory use console
            if (string.IsNullOrWhiteSpace(outFilePath))
                return Console.Out;

            if (Directory.Exists(outFilePath) && !string.IsNullOrEmpty(filename))
            {
                return File.CreateText(Path.Combine(outFilePath, filename));
            }

            return File.CreateText(outFilePath);
        }

        private static string GetFilename(IAssembly assembly, WriterType writer, SyntaxWriterType syntax)
        {
            string name = assembly.Name.Value;
            switch (writer)
            {
                case WriterType.DocIds:
                case WriterType.TypeForwards:
                    return name + ".txt";

                case WriterType.TypeList:
                case WriterType.CSDecl:
                default:
                    switch (syntax)
                    {
                        case SyntaxWriterType.Xml:
                            return name + ".xml";
                        case SyntaxWriterType.Html:
                            return name + ".html";
                        case SyntaxWriterType.Text:
                        default:
                            return name + ".cs";
                    }
            }
        }

        private static ICciFilter GetFilter(string apiList, bool all, bool apiOnly, string excludeApiList, bool excludeMembers, string excludeAttributesList)
        {
            ICciFilter includeFilter = null;

            if (string.IsNullOrWhiteSpace(apiList))
            {
                if (all)
                {
                    includeFilter = new IncludeAllFilter();
                }
                else
                {
                    includeFilter = new PublicOnlyCciFilter(excludeAttributes: apiOnly);
                }
            }
            else
            {
                includeFilter = new DocIdIncludeListFilter(apiList);
            }

            if (!string.IsNullOrWhiteSpace(excludeApiList))
            {
                includeFilter = new IntersectionFilter(includeFilter, new DocIdExcludeListFilter(excludeApiList, excludeMembers));
            }

            if (!string.IsNullOrWhiteSpace(excludeAttributesList))
            {
                includeFilter = new IntersectionFilter(includeFilter, new ExcludeAttributesFilter(excludeAttributesList));
            }

            return includeFilter;
        }

        private static IStyleSyntaxWriter GetSyntaxWriter(TextWriter output, WriterType writer, SyntaxWriterType syntax)
        {
            if (writer != WriterType.CSDecl && writer != WriterType.TypeList)
                return null;

            switch (syntax)
            {
                case SyntaxWriterType.Xml:
                    return new OpenXmlSyntaxWriter(output);
                case SyntaxWriterType.Html:
                    return new HtmlSyntaxWriter(output);
                case SyntaxWriterType.Text:
                default:
                    return new TextSyntaxWriter(output) { SpacesInIndent = 4 };
            }
        }

        private static string GetAssemblyVersion() => typeof(Program).Assembly.GetName().Version.ToString();
    }
}
