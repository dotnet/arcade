using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.CoreFxTesting
{
    /// <summary>
    /// A class representing a CoreFX test assembly to be run
    /// </summary>
    public class XUnitTestAssembly
    {
        [JsonRequired]
        [JsonProperty("name")]
        public string Name;

        [JsonRequired]
        [JsonProperty("enabled")]
        public bool IsEnabled;

        [JsonRequired]
        [JsonProperty("exclusions")]
        public Exclusions Exclusions;

        // Used to assign a test url or to override it via the json file definition - mark it as optional in the test definition
        [JsonIgnore]
        [JsonProperty(Required = Required.Default)]
        public string Url;

    }
    /// <summary>
    /// Class representing a collection of test exclusions
    /// </summary>
    public class Exclusions
    {
        [JsonProperty("namespaces")]
        public Exclusion[] Namespaces;

        [JsonProperty("classes")]
        public Exclusion[] Classes;

        [JsonProperty("methods")]
        public Exclusion[] Methods;
    }

    /// <summary>
    /// Class representing a single test exclusion
    /// </summary>
    public class Exclusion
    {
        [JsonRequired]
        [JsonProperty("name", Required = Required.DisallowNull)]
        public string Name;

        [JsonRequired]
        [JsonProperty("reason", Required = Required.DisallowNull)]
        public string Reason;
    }

    public class GenerateRSPFile : Task
    {
        [Required]
        public string InputFile { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            Dictionary<string, XUnitTestAssembly> testAssemblyDefinitions = DeserializeTestJson(InputFile);

            GenerateRSP(testAssemblyDefinitions, OutputFile);

            return true;
        }

        /// <summary>
        /// Deserialize a list of JSON objects defining test assemblies
        /// </summary>
        /// <param name="testDefinitionFilePath">The path on disk to the test list. The test list must conform to a schema generated from XUnitTestAssembly</param>
        /// <returns></returns>
        private Dictionary<string, XUnitTestAssembly> DeserializeTestJson(string testDefinitionFilePath)
        {
            JSchemaGenerator jsonGenerator = new JSchemaGenerator();

            // Generate a JSON schema from the XUnitTestAssembly class against which to validate the test list
            JSchema testDefinitionSchema = jsonGenerator.Generate(typeof(IList<XUnitTestAssembly>));
            IList<XUnitTestAssembly> testAssemblies = new List<XUnitTestAssembly>();

            IList<string> validationMessages = new List<string>();

            using (var sr = new StreamReader(testDefinitionFilePath))
            using (var jsonReader = new JsonTextReader(sr))
            using (var jsonValidationReader = new JSchemaValidatingReader(jsonReader))
            {
                // Create schema validator
                jsonValidationReader.Schema = testDefinitionSchema;
                jsonValidationReader.ValidationEventHandler += (o, a) => validationMessages.Add(a.Message);

                // Deserialize json test assembly definitions
                JsonSerializer serializer = new JsonSerializer();
                try
                {
                    testAssemblies = serializer.Deserialize<List<XUnitTestAssembly>>(jsonValidationReader);
                }
                catch (JsonSerializationException ex)
                {
                    // Invalid definition
                    throw new AggregateException(ex);
                }
            }

            if (validationMessages.Count != 0)
            {
                StringBuilder aggregateExceptionMessage = new StringBuilder();
                foreach (string validationMessage in validationMessages)
                {
                    aggregateExceptionMessage.Append("JSON Validation Error: ");
                    aggregateExceptionMessage.Append(validationMessage);
                    aggregateExceptionMessage.AppendLine();
                }

                throw new AggregateException(new JSchemaValidationException(aggregateExceptionMessage.ToString()));

            }
            // Generate a map of test assembly names to their object representations - this is used to download and match them to their on-disk representations
            var nameToTestAssemblyDef = new Dictionary<string, XUnitTestAssembly>();

            // Map test names to their definitions
            foreach (XUnitTestAssembly assembly in testAssemblies)
            {
                // Filter disabled tests
                if (assembly.IsEnabled)
                {
                    nameToTestAssemblyDef.Add(assembly.Name, assembly);
                }
                // TODO: is this needed anywhere?
                //else
                //{
                //    disabledTests.Add(assembly.Name);
                //}
            }

            return nameToTestAssemblyDef;
        }

        /// <summary>
        /// Generate an rsp file from an XUnitTestAssembly class
        /// </summary>
        /// <param name="testDefinition">The XUnitTestAssembly object parsed from a specified test list</param>
        /// <param name="outputPath">Path to which to output a .rsp file</param>
        private void GenerateRSP(Dictionary<string, XUnitTestAssembly> testAssemblyDefinitions, string rspFilePath)
        {
            if (File.Exists(rspFilePath))
                File.Delete(rspFilePath);

            // Write RSP file to disk
            using (StreamWriter sr = File.CreateText(rspFilePath))
            {
                foreach (XUnitTestAssembly testDefinition in testAssemblyDefinitions.Values)
                {
                    // If no exclusions are defined, we don't need to generate an .rsp file
                    if (testDefinition.Exclusions == null)
                        return;

                    // Method exclusions
                    if (testDefinition.Exclusions.Methods != null)
                    {
                        foreach (Exclusion exclusion in testDefinition.Exclusions.Methods)
                        {
                            if (String.IsNullOrWhiteSpace(exclusion.Name))
                                continue;
                            sr.Write("-skipmethod ");
                            sr.Write(exclusion.Name);
                            sr.WriteLine();
                        }
                    }

                    // Class exclusions
                    if (testDefinition.Exclusions.Classes != null)
                    {
                        foreach (Exclusion exclusion in testDefinition.Exclusions.Classes)
                        {
                            if (String.IsNullOrWhiteSpace(exclusion.Name))
                                continue;
                            sr.Write("-skipclass ");
                            sr.Write(exclusion.Name);
                            sr.WriteLine();
                        }

                    }

                    // Namespace exclusions
                    if (testDefinition.Exclusions.Namespaces != null)
                    {
                        foreach (Exclusion exclusion in testDefinition.Exclusions.Namespaces)
                        {
                            if (String.IsNullOrWhiteSpace(exclusion.Name))
                                continue;
                            sr.Write("-skipnamespace ");
                            sr.Write(exclusion.Name);
                            sr.WriteLine();
                        }
                    }
                }
            }
        }
    }
}
