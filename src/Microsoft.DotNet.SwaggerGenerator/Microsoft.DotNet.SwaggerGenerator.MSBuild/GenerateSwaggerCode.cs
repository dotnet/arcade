using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.SwaggerGenerator.Modeler;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.SwaggerGenerator.MSBuild
{
    public class GenerateSwaggerCode : Task
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

        private async System.Threading.Tasks.Task ExecuteAsync()
        {
            var options = new GeneratorOptions
            {
                LanguageName = "csharp",
                Namespace = RootNamespace,
                ClientName = ClientName
            };

            Log.LogMessage(MessageImportance.Low, $"Reading swagger document {SwaggerDocumentUri}");
            SwaggerDocument document = await GetSwaggerDocument(SwaggerDocumentUri);

            Log.LogMessage(MessageImportance.Low, $"Generating client code model");
            var generator = new ServiceClientModelFactory(options);
            ServiceClientModel model = generator.Create(document);

            Log.LogMessage(MessageImportance.Low, $"Generating code files for language '{options.LanguageName}'");
            var codeFactory = new ServiceClientCodeFactory();
            List<CodeFile> code = codeFactory.GenerateCode(model, options, new MSBuildLogger(Log));

            Log.LogMessage(MessageImportance.High, $"Generating {SwaggerDocumentUri} -> {OutputDirectory}");
            var outputDirectory = new DirectoryInfo(OutputDirectory);
            outputDirectory.Create();

            var generatedFiles = new List<ITaskItem>();
            foreach ((string path, string contents) in code)
            {
                string fullPath = Path.Combine(outputDirectory.FullName, path);
                var file = new FileInfo(fullPath);
                file.Directory.Create();
                Log.LogMessage(MessageImportance.Normal, $"Writing file '{file.FullName}'");
                File.WriteAllText(file.FullName, contents);
                generatedFiles.Add(new TaskItem(file.FullName));
            }

            GeneratedFiles = generatedFiles.ToArray();
        }

        private static async Task<SwaggerDocument> GetSwaggerDocument(string input)
        {
            using (var client = new HttpClient())
            {
                using (Stream docStream = await client.GetStreamAsync(input))
                using (var reader = new StreamReader(docStream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    return SwaggerSerializer.Deserialize(jsonReader);
                }
            }
        }
    }
}
