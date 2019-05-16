using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Microsoft.DotNet.Arcade.Sdk
{
#if NET472
    [LoadInSeparateAppDomain]
    public class InstallDotNetCore : AppDomainIsolatedTask
    {
        static InstallDotNetCore() => AssemblyResolution.Initialize();
#else
    public class InstallDotNetCore : Task
    {
#endif
        public string VersionsPropsPath { get; set; }

        [Required]
        public string DotNetInstallScript { get; set; }
        [Required]
        public string GlobalJsonPath { get; set; }
        public string Platform { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(GlobalJsonPath))
            {
                Log.LogWarning($"Unable to find global.json file '{GlobalJsonPath} exiting");
                return true;
            }
            if (!File.Exists(DotNetInstallScript))
            {
                Log.LogError($"Unable to find dotnet install script '{DotNetInstallScript} exiting");
                return !Log.HasLoggedErrors;
            }

            var jsonContent = File.ReadAllText(GlobalJsonPath);
            var bytes = Encoding.UTF8.GetBytes(jsonContent);

            using (JsonDocument jsonDocument = JsonDocument.Parse(bytes))
            {
                if (jsonDocument.RootElement.TryGetProperty("tools", out JsonElement toolsElement))
                {
                    if (toolsElement.TryGetProperty("runtimes", out JsonElement dotnetLocalElement))
                    {
                        var runtimeItems = new Dictionary<string, IEnumerable<KeyValuePair<string, string>>>();
                        foreach (var runtime in dotnetLocalElement.EnumerateObject())
                        {
                            var items = GetItemsFromJsonElementArray(runtime, out string runtimeName);
                            runtimeItems.Add(runtimeName, items);
                        }
                        if (runtimeItems.Count > 0)
                        {
                            System.Linq.ILookup<string, ProjectProperty> properties = null;
                            // Only load Versions.props if there's a need to look for a version identifier (ie, there's a value listed that's not a parsable version).
                            if (runtimeItems.SelectMany(r => r.Value).Select(r => r.Key).FirstOrDefault(f => !SemanticVersion.TryParse(f, out SemanticVersion version)) != null)
                            {
                                if (!File.Exists(VersionsPropsPath))
                                {
                                    Log.LogError($"Unable to find translation file {VersionsPropsPath}");
                                    return !Log.HasLoggedErrors;
                                }
                                else
                                {
                                    var proj = Project.FromFile(VersionsPropsPath, new Build.Definition.ProjectOptions());
                                    properties = proj.AllEvaluatedProperties.ToLookup(p => p.Name, StringComparer.OrdinalIgnoreCase );
                                }
                            }
                            
                            foreach(var runtimeItem in runtimeItems)
                            {
                                foreach(var item in runtimeItem.Value)
                                {
                                    SemanticVersion version = null;
                                    // Try to parse version
                                    if (!SemanticVersion.TryParse(item.Key, out version))
                                    {
                                        var propertyName = item.Key.Trim('$', '(', ')');

                                        // Unable to parse version, try to find the corresponding identifer from the MSBuild loaded MSBuild properties
                                        string evaluatedValue = properties[propertyName].First().EvaluatedValue;
                                        if (!SemanticVersion.TryParse(evaluatedValue, out version))
                                        {
                                            Log.LogError($"Unable to parse '{item.Key}' from properties defined in '{VersionsPropsPath}'");
                                        }
                                    }

                                    if(version != null)
                                    {
                                        string arguments = $"-runtime \"{runtimeItem.Key}\" -version \"{version.ToNormalizedString()}\"";
                                        string architecture = item.Value;
                                        if (!string.IsNullOrWhiteSpace(architecture))
                                        {
                                            arguments += $" -architecture {architecture}";
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrWhiteSpace(Platform) && !string.Equals(Platform, "AnyCpu", StringComparison.OrdinalIgnoreCase))
                                            {
                                                arguments += $" -architecture {Platform}";
                                            }
                                            else if (RuntimeInformation.OSArchitecture == Architecture.X86 ||
                                                     RuntimeInformation.OSArchitecture == Architecture.X64)
                                            {
                                                arguments += " -architecture x64";
                                            }
                                        }
                                        Log.LogMessage(MessageImportance.Low, $"Executing: {DotNetInstallScript} {arguments}");
                                        var process = Process.Start(new ProcessStartInfo()
                                        {
                                            FileName = DotNetInstallScript,
                                            Arguments = arguments,
                                            UseShellExecute = false
                                        });
                                        process.WaitForExit();
                                        if(process.ExitCode != 0)
                                        {
                                            Log.LogError("dotnet-install failed");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return !Log.HasLoggedErrors;
        }

        /*
         * Parses a json token of this format
         * { (runtime): [(version), ..., (version)] }
         * or this format
         * { (runtime/architecture): [(version), ..., (version)] }
         */
        private IEnumerable<KeyValuePair<string, string>> GetItemsFromJsonElementArray(JsonProperty token, out string runtime)
        {
            var items = new List<KeyValuePair<string, string>>();

            runtime = token.Name;
            string architecture = string.Empty;
            if(runtime.Contains('/'))
            {
                var parts = runtime.Split(new char[] { '/' }, 2);
                runtime = parts[0];
                architecture = parts[1];
            }
            foreach (var version in token.Value.EnumerateArray())
            {
                items.Add(new KeyValuePair<string, string>(version.GetString(), architecture));
            }
            return items.ToArray();
        }
    }
}
