// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.VisualStudio
{
    public sealed class GeneratePkgDef : Task
    {
        public bool OverwriteUnchangedFile { get; set; }

        [Required]
        public string OutputFile { get; set; }

        [Required]
        public ITaskItem[] Keys { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            var builder = new StringBuilder();

            // TODO: better handling of incorrect inputs

            foreach (var key in Keys)
            {
                var xml = XDocument.Load(new StringReader("<Values>" + key.GetMetadata("Values") + "</Values>"));

                builder.AppendLine("[" + Path.Combine("$RootKey$", key.ItemSpec).Replace('/', '\\') + "]");

                foreach (var valueElement in xml.Root.Elements("Value"))
                {
                    var value = valueElement.Value;

                    if (!string.IsNullOrEmpty(value))
                    {
                        var name = valueElement.Attribute("Name")?.Value;
                        name = name is null ? "@" : "\"" + name.Replace("\"", "\"\"") + "\"";

                        var type = valueElement.Attribute("Type")?.Value;

                        if (string.Equals(type, "int", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(value, out var intValue))
                            {
                                value = "dword:" + intValue.ToString("X8");
                            }
                            else
                            {
                                Log.LogError($"Specified value is not a valid integer: {valueElement}");
                            }
                        }
                        else if (string.Equals(type, "bool", StringComparison.OrdinalIgnoreCase))
                        {
                            if (bool.TryParse(value, out var boolValue))
                            {
                                value = "dword:" + (boolValue ? 1 : 0).ToString("X8");
                            }
                            else
                            {
                                Log.LogError($"Specified value is neither 'true' nor 'false': {valueElement}");
                            }
                        }
                        else if (string.Equals(type, "resource", StringComparison.OrdinalIgnoreCase))
                        {
                            if (ushort.TryParse(value, out var intValue))
                            {
                                value = "\"#" + intValue + "\"";
                            }
                            else
                            {
                                Log.LogError($"Specified value is not a valid resource id (ushort): {valueElement}");
                            }
                        }
                        else if (string.Equals(type, "guid", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Guid.TryParse(value, out var guidValue))
                            {
                                value = "\"" + guidValue.ToString("B") + "\"";
                            }
                            else
                            {
                                Log.LogError($"Specified value is not a valid GUID: {valueElement}");
                            }
                        }
                        else 
                        {
                            if (!string.IsNullOrEmpty(type) && !string.Equals(type, "string", StringComparison.OrdinalIgnoreCase))
                            {
                                Log.LogError($"Invalid type: {valueElement}");
                            }

                            value = "\"" + value.Replace("\"", "\"\"") + "\"";
                        }

                        builder.AppendLine(name + "=" + value);
                    }
                }
            }

            if (!Log.HasLoggedErrors)
            {
                WritePkgDefFile(builder.ToString());
            }
        }

        private void WritePkgDefFile(string content)
        {
            try
            {
                if (!OverwriteUnchangedFile && File.Exists(OutputFile))
                {
                    var originalContent = File.ReadAllText(OutputFile);
                    if (originalContent == content)
                    {
                        // Don't rewrite the file if the contents are the same
                        return;
                    }
                }

                File.WriteAllText(OutputFile, content);
            }
            catch (Exception e)
            {
                Log.LogError($"Error writing to file '{OutputFile}': {e.Message}");
            }
        }
    }
}
