using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using SwaggerGenerator;
using SwaggerGenerator.csharp;
using SwaggerGenerator.Modeler;
using Swashbuckle.AspNetCore.Swagger;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.SwaggerGenerator.MSBuild
{
    public class GenerateSwaggerCode : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string SwaggerDocumentUri { get; set; }

        [Required]
        public string RootNamespace { get; set; }

        [Required]
        public string ClientName { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        [Output]
        public ITaskItem[] GeneratedFiles { get; set; }

        public override bool Execute()
        {
            try
            {
                ExecuteAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true, true, null);
            }

            return !Log.HasLoggedErrors;
        }

        private async Task ExecuteAsync()
        {
            var options = new GeneratorOptions
            {
                LanguageName = "csharp",
                Namespace = RootNamespace,
                ClientName = ClientName,
            };

            var document = await GetSwaggerDocument(SwaggerDocumentUri);

            var generator = new ServiceClientModelFactory(options);
            var model = generator.Create(document);

            var codeFactory = new ServiceClientCodeFactory();
            var code = codeFactory.GenerateCode(model, options);

            var outputDirectory = new DirectoryInfo(OutputDirectory);
            outputDirectory.Create();

            var generatedFiles = new List<ITaskItem>();
            foreach (var (path, contents) in code)
            {
                var fullPath = Path.Combine(outputDirectory.FullName, path);
                var file = new FileInfo(fullPath);
                file.Directory.Create();
                Log.LogMessage(MessageImportance.Normal, $"Generating file '{file.FullName}'");
                File.WriteAllText(file.FullName, contents);
                generatedFiles.Add(new TaskItem(file.FullName));
            }

            GeneratedFiles = generatedFiles.ToArray();
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
