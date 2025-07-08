// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Installers
{
    public class CreateWixBuildWixpack : Task
    {
        public ITaskItem BindTrackingFile { get; set; }

        public string[] Cultures { get; set; }

        public string[] DefineConstants { get; set; }

        public ITaskItem[] Extensions { get; set; }

        public string[] IncludeSearchPaths { get; set; }

        public string InstallerPlatform { get; set; }

        [Required]
        public string InstallerFile { get; set; }

        [Required]
        public ITaskItem IntermediateDirectory { get; set; }

        /// <summary>
        /// path of wixpackage file
        /// </summary>
        [Output]
        public string OutputFile { get; set; }

        /// <summary>
        /// folder to place wixpackage output file
        /// </summary>
        [Required]
        public string OutputFolder { get; set; }

        public string OutputType { get; set; }

        public ITaskItem PdbFile { get; set; }

        public string PdbType { get; set; }

        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        [Required]
        public string WixpackWorkingDir { get; set; }

        private Dictionary<string, string> _defineConstantsDictionary;
        private string _wixprojDir;
        private string _installerFilename;

        private const string _packageExtension = ".wixpack.zip";

        public override bool Execute()
        {
            Console.WriteLine("Executing CreateWixBuildWixpack Task...");
            _defineConstantsDictionary = GetDefineConstantsDictionary();

            if (string.IsNullOrWhiteSpace(WixpackWorkingDir))
            {
                WixpackWorkingDir = Path.Combine(Path.GetTempPath(), "WixpackTemp", Guid.NewGuid().ToString().Split('-')[0]);
            }
            
            _wixprojDir = string.Empty;

            if (!_defineConstantsDictionary.TryGetValue("ProjectDir", out _wixprojDir))
            {
                throw new InvalidOperationException("ProjectDir not defined in DefineConstants. Task cannot proceed.");
            }

            _installerFilename = Path.GetFileName(InstallerFile);

            if (Directory.Exists(WixpackWorkingDir))
            {
                Directory.Delete(WixpackWorkingDir, true);
            }
            Directory.CreateDirectory(WixpackWorkingDir);

            CopySourceFilesAndContent();

            // Copy wixproj - fail if ProjectPath is not defined
            if (_defineConstantsDictionary.TryGetValue("ProjectPath", out var projectPath))
            {
                string destPath = Path.Combine(WixpackWorkingDir, Path.GetFileName(projectPath));
                File.Copy(projectPath, destPath, overwrite: true);
            }
            else
            {
                Console.WriteLine("ProjectPath not defined in DefineConstants. Unexpected - failure!");
                // Throw and exception here
            }

            // copy .sln file if it exists
            if (_defineConstantsDictionary.TryGetValue("SolutionPath", out var solutionPath))
            {
                string destPath = Path.Combine(WixpackWorkingDir, Path.GetFileName(solutionPath));
                File.Copy(solutionPath, destPath, overwrite: true);
            }

            CopyExtensions();
            CopyIncludeSearchPathsContents();
            UpdatePaths();
            GenerateWixBuildCommandLineFile();
            CreateWixpackPackage();

            return true;
        }

        private void CopyExtensions()
        {
            for (int i = 0; i < Extensions.Length; i++)
            {
                var extension = Extensions[i];
                if (string.IsNullOrWhiteSpace(extension.ItemSpec))
                    continue;

                string filename = Path.GetFileName(extension.ItemSpec);
                CopySourceFile(filename, extension.ItemSpec);

                // Update the extension item spec to just the file name
                Extensions[i] = new TaskItem(Path.Combine(filename, filename));
            }
        }

        private void CreateWixpackPackage()
        {
            OutputFile = Path.Combine(OutputFolder, $"{_installerFilename}{_packageExtension}");
            if (File.Exists(OutputFile))
            {
                File.Delete(OutputFile);
            }
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }
            ZipFile.CreateFromDirectory(WixpackWorkingDir, OutputFile);
        }

        private void CopyIncludeSearchPathsContents()
        {
            if (IncludeSearchPaths == null || IncludeSearchPaths.Length == 0)
                return;

            for (int i = 0; i < IncludeSearchPaths.Length; i++)
            {
                var includePath = IncludeSearchPaths[i];
                if (string.IsNullOrWhiteSpace(includePath))
                    continue;

                // If not rooted, resolve relative to _wixprojDir
                var fullSourceDir = Path.IsPathRooted(includePath)
                    ? includePath
                    : Path.Combine(_wixprojDir, includePath);

                if (!Directory.Exists(fullSourceDir))
                {
                    Console.WriteLine($"IncludeSearchPath directory not found: {fullSourceDir}");
                    continue;
                }

                // Use a random directory name for the destination
                var randomDirName = Path.GetRandomFileName();
                var destDir = Path.Combine(WixpackWorkingDir, randomDirName);

                CopyDirectoryRecursive(fullSourceDir, destDir);

                // Update IncludeSearchPaths element to the random directory name only
                IncludeSearchPaths[i] = randomDirName;
            }
        }

        private void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destSubDir);
            }
        }

        private void UpdatePaths()
        {
            // Update ProjectDir to just '.'
            if (_defineConstantsDictionary.ContainsKey("ProjectDir"))
            {
                _defineConstantsDictionary["ProjectDir"] = ".";
            }

            // Update ProjectPath to just the project file name
            if (_defineConstantsDictionary.ContainsKey("ProjectPath"))
            {
                _defineConstantsDictionary["ProjectPath"] = Path.GetFileName(_defineConstantsDictionary["ProjectPath"]);
            }

            // Update SolutionDir to just '.'
            if (_defineConstantsDictionary.ContainsKey("SolutionDir"))
            {
                _defineConstantsDictionary["SolutionDir"] = ".";
            }

            // Update SolutionPath to just the solution file name
            if (_defineConstantsDictionary.ContainsKey("SolutionPath"))
            {
                _defineConstantsDictionary["SolutionPath"] = Path.GetFileName(_defineConstantsDictionary["SolutionPath"]);
            }

            // Update OutDir to just '.''
            if (_defineConstantsDictionary.ContainsKey("OutDir"))
            {
                _defineConstantsDictionary["OutDir"] = "%outputfolder%";
            }

            // Update TargetDir to just '.''
            if (_defineConstantsDictionary.ContainsKey("TargetDir"))
            {
                _defineConstantsDictionary["TargetDir"] = "%outputfolder%";
            }

            // Update TargetPath to %outputfolder%\<target file name>
            if (_defineConstantsDictionary.ContainsKey("TargetPath"))
            {
                _defineConstantsDictionary["TargetPath"] = Path.Combine("%outputfolder%", Path.GetFileName(_defineConstantsDictionary["TargetPath"]));
            }

            // Update InstallerFile to %outputfolder%\<installer filename>
            InstallerFile = Path.Combine("%outputfolder%", Path.GetFileName(InstallerFile));

            // Update IntermediateDirectory to %outputfolder%
            IntermediateDirectory.ItemSpec = "%outputfolder%";

            // Update PdbFile to %outputfolder%\<pdb file name>
            if (PdbFile != null && !string.IsNullOrEmpty(PdbFile.ItemSpec))
            {
                PdbFile.ItemSpec = Path.Combine("%outputfolder%", Path.GetFileName(PdbFile.ItemSpec));
            }

            // Update BindTrackingFile to %outputfolder%\<bind tracking file name>
            if (BindTrackingFile != null && !string.IsNullOrEmpty(BindTrackingFile.ItemSpec))
            {
                BindTrackingFile.ItemSpec = Path.Combine("%outputfolder%", Path.GetFileName(BindTrackingFile.ItemSpec));
            }


        }

        private void GenerateWixBuildCommandLineFile()
        {
            var commandLineArgs = new List<string>();

            // Add InstallerPlatform if specified
            if (!string.IsNullOrEmpty(InstallerPlatform))
            {
                commandLineArgs.Add($"-platform {InstallerPlatform}");
            }

            commandLineArgs.Add($"-out {InstallerFile}");

            // Add OutputType if specified
            if (!string.IsNullOrEmpty(OutputType))
            {
                commandLineArgs.Add($"-outputType {OutputType}");
            }

            // Add PdbFile if specified
            if (PdbFile != null && !string.IsNullOrEmpty(PdbFile.ItemSpec))
            {
                commandLineArgs.Add($"-pdb {PdbFile.ItemSpec}");
            }

            // Add PdbType if specified
            if (!string.IsNullOrEmpty(PdbType))
            {
                commandLineArgs.Add($"-pdbType {PdbType}");
            }

            // Add each culture from Cultures array
            if (Cultures != null && Cultures.Length > 0)
            {
                foreach (var culture in Cultures)
                {
                    commandLineArgs.Add($"-culture {culture}");
                }
            }

            // Add all define constants from dictionary
            if (_defineConstantsDictionary != null && _defineConstantsDictionary.Count > 0)
            {
                foreach (var kvp in _defineConstantsDictionary)
                {
                    // Escape strings only if there is a space in the value
                    string kv = $"{kvp.Key}={kvp.Value}";
                    commandLineArgs.Add($"-d {(kv.Contains(' ') ? $"\"{kv}\"" : kv)}");
                }
            }

            // Add IncludeSearchPaths
            if (IncludeSearchPaths != null && IncludeSearchPaths.Length > 0)
            {
                foreach (var includePath in IncludeSearchPaths)
                {
                    commandLineArgs.Add($"-I {includePath}");
                }
            }

            // Add Extensions
            if (Extensions != null)
            {
                foreach (var extension in Extensions)
                {
                    commandLineArgs.Add($"-ext {extension.ItemSpec}");
                }
            }

            // Add IntermediateDirectory
            commandLineArgs.Add($"-intermediatefolder {IntermediateDirectory.ItemSpec}");

            // Add BindTrackingFile if specified
            if (BindTrackingFile != null && !string.IsNullOrEmpty(BindTrackingFile.ItemSpec))
            {
                commandLineArgs.Add($"-trackingfile {BindTrackingFile.ItemSpec}");
            }

            commandLineArgs.Add($"-nologo");
            commandLineArgs.Add($"-wx");

            // Add SourceFiles
            if (SourceFiles != null && SourceFiles.Length > 0)
            {
                foreach (var sourceFile in SourceFiles)
                {
                    commandLineArgs.Add($"{Path.GetFileName(sourceFile.ItemSpec)}");
                }
            }

            string commandLine = "C:\\wix5\\tools.fx\\wix.exe build " + string.Join(" ", commandLineArgs);
            Console.WriteLine("Generated command line: " + commandLine);

            StringBuilder createCmdFileContents = new StringBuilder();
            createCmdFileContents.AppendLine("@echo off");
            createCmdFileContents.AppendLine("set outputfolder=%1");
            createCmdFileContents.AppendLine("if \"%outputfolder%\" NEQ \"\" (");
            createCmdFileContents.AppendLine("  if \"%outputfolder:~-1%\" NEQ \"\\\" ( ");
            createCmdFileContents.AppendLine("    set outputfolder=%outputfolder%\\");
            createCmdFileContents.AppendLine("  )");
            createCmdFileContents.AppendLine(")");
            createCmdFileContents.AppendLine("REM Wix build command");
            createCmdFileContents.AppendLine(commandLine);
            File.WriteAllText(Path.Combine(WixpackWorkingDir, "create.cmd"), createCmdFileContents.ToString());
        }

        /// <summary>
        /// Converts the DefineConstants string array (format: key=value) to a Dictionary.
        /// </summary>
        private Dictionary<string, string> GetDefineConstantsDictionary()
        {
            var dict = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (DefineConstants == null)
                return dict;

            foreach (var entry in DefineConstants)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;
                var idx = entry.IndexOf('=');
                if (idx < 0)
                    continue;
                var key = entry.Substring(0, idx).Trim();
                var value = entry.Substring(idx + 1).Trim();
                if (!string.IsNullOrEmpty(key))
                    dict[key] = value;
            }
            return dict;
        }

        /// <summary>
        /// For each item in SourceFiles, reads the XML, finds all File elements, gets File@Id and File@Source values.
        /// If File@Source contains $(<value>), replaces it with the value from _defineConstantsDictionary.
        /// Creates a subfolder in WixpackWorkingDir with the name equal to File@Id value.
        /// </summary>
        private void CopySourceFilesAndContent()
        {
            if (SourceFiles == null || _defineConstantsDictionary == null || string.IsNullOrEmpty(WixpackWorkingDir))
                throw new InvalidOperationException("Task not initialized. Run Execute() first.");

            foreach (var sourceFile in SourceFiles)
            {
                var xmlPath = sourceFile.ItemSpec;

                // if resolvedSource is relative, resolve it against the project directory
                if (!Path.IsPathRooted(xmlPath))
                {
                    xmlPath = Path.Combine(_wixprojDir, xmlPath);
                }

                if (!File.Exists(xmlPath))
                {
                    Console.WriteLine($"Source XML not found: {xmlPath}");
                    continue;
                }

                // Copy the sourceFile to WixpackWorkingDir
                var copiedXmlPath = Path.Combine(WixpackWorkingDir, Path.GetFileName(xmlPath));
                File.Copy(xmlPath, copiedXmlPath, overwrite: true);

                try
                {
                    var doc = XDocument.Load(copiedXmlPath);

                    var contentElements = new (string, string, string[])[]
                    {
                        ("File", "Id", ["Source"]),
                        ("MsiPackage", "Id", ["SourceFile"]),
                        ("ExePackage", "Id", ["SourceFile"]),
                        ("Payload", "Id", ["SourceFile"]),
                        ("WixStandardBootstrapperApplication", "Id", ["LicenseFile", "LocalizationFile", "ThemeFile"]),
                        ("WixVariable", "Id", ["Value"]),
                        ("Icon", "Id", ["SourceFile"])
                    };

                    foreach (var (elementName, idAttr, sourceAttrArray) in contentElements)
                    {
                        var elements = doc.Descendants().Where(e => e.Name.LocalName == elementName);
                        foreach (var element in elements)
                        {
                            foreach (var sourceAttr in sourceAttrArray)
                            {
                                var source = element.Attribute(sourceAttr)?.Value;

                                if (string.IsNullOrEmpty(source))
                                {
                                    continue;
                                }

                                source = ResolvePath(source);

                                // Handle source-file using preprocessor variables
                                // replace them with "*"
                                if (source.Contains("*"))
                                {
                                    // Enumerate all files
                                    // Properly update SourceFile and Id attributes
                                    // if source is relative, resolve it against the project directory
                                    if (!Path.IsPathRooted(source))
                                    {
                                        source = Path.Combine(_wixprojDir, source);
                                    }

                                    // Split source string by "\\*\\" to handle multiple files
                                    var parts = source.Split(new[] { "\\*\\", "\\*" }, StringSplitOptions.RemoveEmptyEntries);
                                    // Enumerate directories  in parts[0] directory and copy each file in source string, replacing the "*" with the enumerated directory name

                                    if (parts.Length != 2)
                                    {
                                        // We only support one specific scenario when path is in the format:
                                        // <directoryPart>\\*\\<filename>
                                        Console.WriteLine($"Invalid source format: {source}");
                                        continue;
                                    }

                                    string directoryPart = parts[0];
                                    // Enumerate directories in the directoryPart
                                    var dirs = Directory.GetDirectories(directoryPart, "*", SearchOption.TopDirectoryOnly);
                                    foreach (var dir in dirs)
                                    {
                                        var filePath = Path.Combine(dir, Path.GetFileName(source));
                                        CopySourceFile(Path.GetFileName(dir), filePath);
                                    }

                                    var originalSource = element.Attribute(sourceAttr)?.Value;
                                    if (!Path.IsPathRooted(originalSource))
                                    {
                                        originalSource = Path.Combine(_wixprojDir, originalSource);
                                    }

                                    // Update the original attribute to "$(token)\\<filename>"
                                    var newTokenizedSourceValue = Path.Combine(Path.GetFileName(Path.GetDirectoryName(originalSource)), Path.GetFileName(originalSource));
                                    element.SetAttributeValue(sourceAttr, newTokenizedSourceValue);
                                }
                                else
                                {
                                    var id = element.Attribute(idAttr)?.Value;
                                    // Resolved source is a single file, copy it to the subfolder
                                    if (string.IsNullOrEmpty(id))
                                    {
                                        id = Path.GetFileName(source);
                                    }

                                    CopySourceFile(id, source);

                                    // Update the original attribute to "<id>\\<filename>"
                                    var newSourceValue = $"{id}\\{Path.GetFileName(source)}";
                                    element.SetAttributeValue(sourceAttr, newSourceValue);
                                }
                            }
                        }
                    }

                    doc.Save(copiedXmlPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {copiedXmlPath}: {ex.Message}");
                }
            }
        }

        private string ResolvePath(string path)
        {
            // Replace $(<value>) with value from _defineConstantsDictionary
            int startIdx = path.IndexOf("$(");
            while (startIdx != -1)
            {
                int endIdx = path.IndexOf(')', startIdx + 2);
                if (endIdx == -1) break;
                var varName = path.Substring(startIdx + 2, endIdx - (startIdx + 2));
                if (_defineConstantsDictionary.TryGetValue(varName, out var varValue))
                {
                    path = path.Substring(0, startIdx) + varValue + path.Substring(endIdx + 1);
                }
                else
                {
                    // Old:
                    // If not found, skip this value
                    //startIdx = path.IndexOf("$(", endIdx + 1);

                    // If not found, replace with "*"
                    path = path.Substring(0, startIdx) + "*" + path.Substring(endIdx + 1);
                }

                startIdx = path.IndexOf("$(");
            }

            return path;
        }

        private void CopySourceFile(string fileId, string source)
        {
            // Create subfolder in WixpackWorkingDir with the name equal to File@Id
            var destDir = Path.Combine(WixpackWorkingDir, fileId);
            Directory.CreateDirectory(destDir);

            // if source is relative, resolve it against the project directory
            if (!Path.IsPathRooted(source))
            {
                source = Path.Combine(_wixprojDir, source);
            }

            // Copy the file if it exists
            if (File.Exists(source))
            {
                var destPath = Path.Combine(destDir, Path.GetFileName(source));
                File.Copy(source, destPath, overwrite: true);
            }
            else
            {
                throw new FileNotFoundException($"Source file not found: {source}");
            }
        }
    }
}
