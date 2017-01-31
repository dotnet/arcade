using System;
using System.Collections.Generic;
using System.CommandLine;
using Autofac;
using JetBrains.Annotations;
using Microsoft.ApiUsageAnalyzer.Cci;
using Microsoft.ApiUsageAnalyzer.Core;

namespace Microsoft.ApiUsageAnalyzer
{
    internal static class Program
    {
        [UsedImplicitly]
        private static void Main(string[] args)
        {
            string command = string.Empty;
            IReadOnlyList<string> inputs = Array.Empty<string>();
            string output = string.Empty;
            string outputDirectory = string.Empty;
            string outputFormat = string.Empty;

            var argSyntax = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineCommand("listOutputFormats", ref command, "List avaliable output formats");

                syntax.DefineCommand("listInputFormats", ref command, "List avaliable output formats");

                syntax.DefineCommand("analyze", ref command, "Analyze inputs");
                syntax.DefineOptionList("i|input", ref inputs, "Input to analyze");
                syntax.DefineOption("o|output", ref output, "Output destination file");
                syntax.DefineOption("output-directory", ref outputDirectory, "Output destination directory");
                syntax.DefineOption("f|format", ref outputFormat, "Output format");
            });

            var container = CreateContainer();


            switch (command)
            {
                case "listOutputFormats":
                    container.Resolve<Application>().ListOutputFormats();
                    break;
                case "listInputFormats":
                    container.Resolve<Application>().ListInputFormats();
                    break;
                case "analyze":
                    if (inputs.Count == 0)
                    {
                        argSyntax.ReportError("fatal: at least one input required");
                    }
                    if (string.IsNullOrEmpty(output) && string.IsNullOrEmpty(outputDirectory))
                    {
                        argSyntax.ReportError("fatal: no output specified");
                    }
                    if (!string.IsNullOrEmpty(output))
                    {
                        container.Resolve<Application>().AnalyzeToFile(inputs, output, outputFormat);
                    }
                    else if (!string.IsNullOrEmpty(outputDirectory))
                    {
                        container.Resolve<Application>().AnalyzeToDirectory(inputs, outputDirectory, outputFormat);
                    }
                    break;
                default:
                    argSyntax.ReportError("fatal: unknown command or option");
                    break;
            }
        }

        private static IContainer CreateContainer()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<JsonOutputFormat>().As<IOutputFormat>();

            builder.RegisterType<DllInputFormat>().As<IInputFormat>();
            builder.RegisterType<DllAnalyzer>();

            builder.RegisterType<CciApiUsageCollector>().As<IApiUsageCollector>();

            builder.RegisterType<Application>();

            return builder.Build();
        }
    }
}