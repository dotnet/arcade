// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateRunScript : Task
    {
        [Required]
        public string[] RunCommands { get; set; }

        [Required]
        public string TemplatePath { get; set; }

        [Required]
        public string OutputPath { get; set; }

        public bool OutputEchoes { get; set; }

        public override bool Execute()
        {
            if (RunCommands.Length == 0)
            {
                Log.LogError("Please provide at least one test command to execute via the RunCommands property.");
                return false;
            }

            if (!File.Exists(TemplatePath))
            {
                Log.LogError($"Runner script template {TemplatePath} was not found.");
                return false;
            }

            string templateContent = File.ReadAllText(TemplatePath);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            Log.LogMessage($"Run commands = {string.Join(Environment.NewLine, RunCommands)}");

            string extension = Path.GetExtension(Path.GetFileName(OutputPath)).ToLowerInvariant();
            switch (extension)
            {
                case ".sh":
                case ".cmd":
                case ".bat":
                    WriteRunScript(templateContent, extension);
                    break;
                default:
                    Log.LogError($"Generating runner scripts with extension '{extension}' is not supported.");
                    return false;
            }

            return true;
        }

        private void WriteRunScript(string templateContent, string extension)
        {
            bool isUnix = extension == ".sh";
            string lineFeed = isUnix ? "\n" : "\r\n";

            var runCommandsBuilder = new StringBuilder();
            foreach (string runCommand in RunCommands)
            {
                runCommandsBuilder.Append($"{runCommand}{lineFeed}");
            }
            templateContent = templateContent.Replace("[[RunCommands]]", runCommandsBuilder.ToString());

            string testRunEchoesText = "";
            if (OutputEchoes)
            {
                var runCommandEchoesBuilder = new StringBuilder();
                foreach (string runCommand in RunCommands)
                {
                    // Escape backtick and question mark characters to avoid running commands instead of echo'ing them.
                    string sanitizedRunCommand = runCommand.Replace("`", "\\`")
                                                           .Replace("?", "\\")
                                                           .Replace("\r","")
                                                           .Replace("\n"," ");

                    if (isUnix)
                    {
                        // Remove parentheses and quotes from echo command before wrapping it in quotes to avoid errors on Linux.
                        sanitizedRunCommand = "\"" + sanitizedRunCommand.Replace("\"", "")
                                           .Replace("(", "")
                                           .Replace(")", "") + "\"";
                    }
                    
                    runCommandEchoesBuilder.Append($"echo {sanitizedRunCommand}{lineFeed}");
                }
                testRunEchoesText = runCommandEchoesBuilder.ToString();
            }
            templateContent = templateContent.Replace("[[RunCommandsEcho]]", testRunEchoesText);

            if (isUnix)
            {
                // Just in case any Windows EOLs have made it in by here, clean any up.
                templateContent = templateContent.Replace("\r\n", "\n");
            }

            using (StreamWriter sw = new StreamWriter(new FileStream(OutputPath, FileMode.Create)))
            {
                sw.NewLine = lineFeed;
                sw.Write(templateContent);
                sw.WriteLine();
            }

            Log.LogMessage($"Wrote {extension} run script to {OutputPath}");
        }
    }
}