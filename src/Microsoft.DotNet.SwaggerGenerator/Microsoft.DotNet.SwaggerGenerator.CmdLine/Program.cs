using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using HandlebarsDotNet;
using Mono.Options;
using SwaggerGenerator.csharp;
using SwaggerGenerator.Languages;
using SwaggerGenerator.Modeler;

namespace SwaggerGenerator
{
    internal static class Program
    {
        private static void Error(string message)
        {
            Console.Error.WriteLine("fatal: " + message);
            Environment.Exit(-1);
        }

        private static void MissingArgument(string name)
        {
            Error($"Missing required argument {name}");
        }

        private static async Task<int> Main(string[] args)
        {
            string input = null;
            string output = null;
            bool version = false;
            bool showHelp = false;
            var generatorOptions = new GeneratorOptions
            {
                LanguageName = "csharp",
                Namespace = "Generated",
                ClientName = "ApiClient",
            };

            var options = new OptionSet
            {
                {"i|input=", "The input swagger spec uri", s => input = s},
                {"o|output=", "The output directory for generated code", o => output = o},
                {"n|ns|namespace=", "The namespace for generated code", n => generatorOptions.Namespace = n},
                {"l|language=", "The language to generate code for", l => generatorOptions.LanguageName = l},
                {"c|client-name=", "The name of the generated client", c => generatorOptions.ClientName = c},
                {"version", "Display the version of this program.", v => version = v != null},
                {"h|?|help", "Display this help message.", h => showHelp = h != null},
            };

            var arguments = options.Parse(args);

            if (version)
            {
                var versionString = Assembly.GetEntryAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    .InformationalVersion;
                Console.WriteLine(versionString);
                return 0;
            }

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
                return 0;
            }

            if (string.IsNullOrEmpty(input))
            {
                MissingArgument(nameof(input));
            }

            if (string.IsNullOrEmpty(output))
            {
                MissingArgument(nameof(output));
            }

            SwaggerDocument document = await GetSwaggerDocument(input);

            var generator = new ServiceClientModelFactory(generatorOptions);
            var model = generator.Create(document);

            var codeFactory = new ServiceClientCodeFactory();
            var code = codeFactory.GenerateCode(model, generatorOptions);

            var outputDirectory = new DirectoryInfo(output);
            outputDirectory.Create();

            foreach (var (path, contents) in code)
            {
                var fullPath = Path.Combine(outputDirectory.FullName, path);
                var file = new FileInfo(fullPath);
                file.Directory.Create();
                File.WriteAllText(file.FullName, contents);
            }

            return 0;
        }

        private static async Task<SwaggerDocument> GetSwaggerDocument(string input)
        {
            using (var client = new HttpClient())
            {
                using (var docStream = await client.GetStreamAsync(input))
                using (var reader = new StreamReader(docStream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    return SwaggerSerializer.Deserialize(jsonReader);
                }
            }
        }
    }
}
