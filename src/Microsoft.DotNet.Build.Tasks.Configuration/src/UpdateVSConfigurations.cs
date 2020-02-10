using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Evaluation;
using System.Linq;
using Microsoft.Build.Construction;
using System.Text;
using System.Xml;

namespace Microsoft.DotNet.Build.Tasks.Configuration
{
    public class UpdateVSConfigurations : BuildTask
    {
        public ITaskItem[] ProjectsToUpdate { get; set; }

        public ITaskItem[] SolutionsToUpdate { get; set; }

        private const string ConfigurationPropsFilename = "Configurations.props";
        private static Regex s_configurationConditionRegex = new Regex(@"'\$\(Configuration\)\|\$\(Platform\)' ?== ?'(?<config>.*)'");
        private static string[] s_configurationSuffixes = new [] { "Debug", "Release" };

        public override bool Execute()
        {
            if (ProjectsToUpdate == null) ProjectsToUpdate = new ITaskItem[0];
            if (SolutionsToUpdate == null) SolutionsToUpdate = new ITaskItem[0];

            foreach (var item in ProjectsToUpdate)
            {
                string projectFile = item.ItemSpec;
                string projectConfigurationPropsFile = Path.Combine(Path.GetDirectoryName(projectFile), ConfigurationPropsFilename);

                string[] expectedConfigurations = s_configurationSuffixes;
                if (File.Exists(projectConfigurationPropsFile))
                {
                    expectedConfigurations = GetConfigurationStrings(projectConfigurationPropsFile);
                }

                Log.LogMessage($"Updating {projectFile}");

                var project = ProjectRootElement.Open(projectFile);
                ICollection<ProjectPropertyGroupElement> propertyGroups = GetPropertyGroupsToRemove(project);
                var actualConfigurations = GetConfigurationsFromProperty(project);

                bool addedGuid = EnsureProjectGuid(project);

                if (!actualConfigurations.SequenceEqual(expectedConfigurations))
                {
                    ReplaceConfigurationsProperty(project, propertyGroups, expectedConfigurations);
                }

                if (addedGuid || !actualConfigurations.SequenceEqual(expectedConfigurations))
                {
                    project.Save();
                }
            }

            foreach (var solution in SolutionsToUpdate)
            {
                UpdateSolution(solution);
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Gets a sorted list of configuration strings from a Configurations.props file
        /// </summary>
        /// <param name="configurationProjectFile">Path to Configuration.props file</param>
        /// <returns>Sorted list of configuration strings</returns>
        private static string[] GetConfigurationStrings(string configurationProjectFile, bool addSuffixes = true)
        {
            var configurationProject = new Project(configurationProjectFile);

            var buildConfigurations = configurationProject.GetPropertyValue("BuildConfigurations");

            ProjectCollection.GlobalProjectCollection.UnloadProject(configurationProject);

            // if starts with _ it is a placeholder configuration and we should ignore it.
            var configurations = buildConfigurations.Trim()
                                      .Split(';')
                                      .Select(c => c.Trim())
                                      .Where(c => !String.IsNullOrEmpty(c) && !c.StartsWith("_"));

            if (addSuffixes)
            {
                configurations = configurations.SelectMany(c => s_configurationSuffixes.Select(s => c + "-" + s));
            }

            return configurations.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>
        /// Gets a collection of a project file's configuration PropertyGroups in the legacy format.
        /// </summary>
        /// <param name="project">Project</param>
        /// <returns>Collection of PropertyGroups that should be removed from the project.</returns>
        private static ICollection<ProjectPropertyGroupElement> GetPropertyGroupsToRemove(ProjectRootElement project)
        {
            List<ProjectPropertyGroupElement> propertyGroups = new List<ProjectPropertyGroupElement>();

            foreach (var propertyGroup in project.PropertyGroups)
            {
                var match = s_configurationConditionRegex.Match(propertyGroup.Condition);

                if (match.Success)
                {
                    propertyGroups.Add(propertyGroup);
                }
            }

            return propertyGroups;
        }

        private string[] GetConfigurationsFromProperty(ProjectRootElement project)
        {
            return project.PropertyGroups
                .SelectMany(g => g.Properties)
                .FirstOrDefault(p => p.Name == "Configurations")?.Value
                .Split(';')
                ?? Array.Empty<string>();
        }

        /// <summary>
        /// Replaces the configurations property with the expected configurations.
        /// Doesn't attempt to preserve any content since it can all be regenerated.
        /// Does attempt to preserve the ordering in the project file.
        /// </summary>
        /// <param name="project">Project</param>
        /// <param name="oldPropertyGroups">PropertyGroups to remove</param>
        /// <param name="newConfigurations"></param>
        private static void ReplaceConfigurationsProperty(ProjectRootElement project, IEnumerable<ProjectPropertyGroupElement> oldPropertyGroups, IEnumerable<string> newConfigurations)
        {
            foreach (var oldPropertyGroup in oldPropertyGroups)
            {
                project.RemoveChild(oldPropertyGroup);
            }

            string configurationsValue = string.Join(";", newConfigurations);
            var configurationsProperty = project.Properties.FirstOrDefault(p => p.Name == "Configurations");
            if (configurationsProperty == null)
            {
                var firstPropertyGroup = project.PropertyGroups.FirstOrDefault();
                if (firstPropertyGroup == null)
                {
                    firstPropertyGroup = project.CreatePropertyGroupElement();
                }

                configurationsProperty = firstPropertyGroup.AddProperty("Configurations", configurationsValue);
            }
            else
            {
                configurationsProperty.Value = configurationsValue;
            }
        }

        private static Dictionary<string, string> _guidMap = new Dictionary<string, string>();

        private bool EnsureProjectGuid(ProjectRootElement project)
        {
            ProjectPropertyElement projectGuid = project.Properties.FirstOrDefault(p => p.Name == "ProjectGuid");
            string guid = string.Empty;

            if (projectGuid != null)
            {
                guid = projectGuid.Value;
                string projectName;
                if (_guidMap.TryGetValue(guid, out projectName))
                {
                    Log.LogMessage($"The ProjectGuid='{guid}' is duplicated across projects '{projectName}' and '{project.FullPath}', so creating a new one for project '{project.FullPath}'");
                    guid = Guid.NewGuid().ToString("B").ToUpper();
                    _guidMap.Add(guid, project.FullPath);
                    projectGuid.Value = guid;
                    return true;
                }
                else
                {
                    _guidMap.Add(guid, project.FullPath);
                }
            }

            if (projectGuid == null)
            {
                guid = Guid.NewGuid().ToString("B").ToUpper();

                var propertyGroup = project.Imports.FirstOrDefault()?.NextSibling as ProjectPropertyGroupElement;

                if (propertyGroup == null || !string.IsNullOrEmpty(propertyGroup.Condition))
                {
                    propertyGroup = project.CreatePropertyGroupElement();
                    ProjectElement insertAfter = project.Imports.FirstOrDefault();

                    if (insertAfter == null)
                    {
                        insertAfter = project.Children.FirstOrDefault();
                    }

                    if (insertAfter != null)
                    {
                        project.InsertAfterChild(propertyGroup, insertAfter);
                    }
                    else
                    {
                        project.AppendChild(propertyGroup);
                    }
                }

                propertyGroup.AddProperty("ProjectGuid", guid);
                return true;
            }

            return false;
        }

        private void UpdateSolution(ITaskItem solutionItem)
        {
            string solutionItemPath = Path.GetFullPath(solutionItem.ItemSpec);
            string solutionDirectory, solutionPath;            
            if (Path.GetExtension(solutionItemPath).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                solutionDirectory = Path.GetFileName(solutionItemPath);
                solutionPath = solutionItemPath;
            }
            else
            {
                solutionDirectory = solutionItemPath;
                string solutionFile = GetNameForSolution(solutionDirectory);
                solutionPath = Path.Combine(solutionDirectory, solutionFile);
            }

            if (!solutionDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                solutionDirectory += Path.DirectorySeparatorChar;
            }

            string projectExclude = solutionItem.GetMetadata("ExcludePattern");
            List<ProjectFolder> projectFolders = new List<ProjectFolder>();


            ProjectFolder testFolder = new ProjectFolder(solutionDirectory, "tests", "{1A2F9F4A-A032-433E-B914-ADD5992BB178}", projectExclude, true);
            if (testFolder.FolderExists)
            {
                projectFolders.Add(testFolder);
            }

            ProjectFolder srcFolder = new ProjectFolder(solutionDirectory, "src", "{E107E9C1-E893-4E87-987E-04EF0DCEAEFD}", projectExclude);
            if (srcFolder.FolderExists)
            {
                testFolder.DependsOn.Add(srcFolder);
                projectFolders.Add(srcFolder);
            };

            ProjectFolder refFolder = new ProjectFolder(solutionDirectory, "ref", "{2E666815-2EDB-464B-9DF6-380BF4789AD4}", projectExclude);
            if (refFolder.FolderExists)
            {
                srcFolder.DependsOn.Add(refFolder);
                projectFolders.Add(refFolder);
            }

            if (projectFolders.Count == 0)
            {
                Log.LogMessage($"Directory '{solutionDirectory}' does not contain a 'src', 'tests', or 'ref' directory so skipping solution generation.");
                return;
            }

            Log.LogMessage($"Generating solution for '{solutionDirectory}'...");

            Solution solution = new Solution(solutionPath);

            StringBuilder slnBuilder = new StringBuilder();
            slnBuilder.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            slnBuilder.AppendLine("# Visual Studio 15");
            slnBuilder.AppendLine("VisualStudioVersion = 15.0.27213.1");
            slnBuilder.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");


            // Output project items
            foreach (var projectFolder in projectFolders)
            {
                foreach (var slnProject in projectFolder.Projects)
                {
                    string projectName = Path.GetFileNameWithoutExtension(slnProject.ProjectPath);
                    // Normalize the directory separators to the windows version given these are projects for VS and only work on windows.
                    string relativePathFromCurrentDirectory = slnProject.ProjectPath.Replace(solutionDirectory, "").Replace("/", "\\");

                    slnBuilder.AppendLine($"Project(\"{slnProject.SolutionGuid}\") = \"{projectName}\", \"{relativePathFromCurrentDirectory}\", \"{slnProject.ProjectGuid}\"");

                    bool writeEndProjectSection = false;
                    foreach (var dependentFolder in projectFolder.DependsOn)
                    {
                        foreach (var depProject in dependentFolder.Projects)
                        {
                            string depProjectId = depProject.ProjectGuid;
                            slnBuilder.AppendLine(
                                $"\tProjectSection(ProjectDependencies) = postProject\r\n\t\t{depProjectId} = {depProjectId}");
                            writeEndProjectSection = true;
                        }
                    }
                    if (writeEndProjectSection)
                    {
                        slnBuilder.AppendLine("\tEndProjectSection");
                    }

                    slnBuilder.AppendLine("EndProject");
                }
            }

            // Output the solution folder items
            foreach (var projectFolder in projectFolders)
            {
                slnBuilder.AppendLine($"Project(\"{projectFolder.SolutionGuid}\") = \"{projectFolder.Name}\", \"{projectFolder.Name}\", \"{projectFolder.ProjectGuid}\"\r\nEndProject");
            }

            string anyCPU = "Any CPU";
            string slnDebug = "Debug|" + anyCPU;
            string slnRelease = "Release|" + anyCPU;

            // Output the solution configurations
            slnBuilder.AppendLine("Global");
            slnBuilder.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            slnBuilder.AppendLine($"\t\t{slnDebug} = {slnDebug}");
            slnBuilder.AppendLine($"\t\t{slnRelease} = {slnRelease}");
            slnBuilder.AppendLine("\tEndGlobalSection");

            slnBuilder.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

            // Output the solution to project configuration mappings
            foreach (var projectFolder in projectFolders)
            {
                foreach (var slnProject in projectFolder.Projects)
                {
                    string projectConfig = slnProject.GetBestConfiguration("netcoreapp-Windows_NT");
                    if (!string.IsNullOrEmpty(projectConfig))
                    {
                        projectConfig += "-";
                    }
                    string[] slnConfigs = new string[] { slnDebug, slnRelease };
                    string[] markers = new string[] { "ActiveCfg", "Build.0" };

                    foreach (string slnConfig in slnConfigs)
                    {
                        foreach (string marker in markers)
                        {
                            slnBuilder.AppendLine($"\t\t{slnProject.ProjectGuid}.{slnConfig}.{marker} = {projectConfig}{slnConfig}");
                        }
                    }
                }
            }

            slnBuilder.AppendLine("\tEndGlobalSection");
            slnBuilder.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
            slnBuilder.AppendLine("\t\tHideSolutionNode = FALSE");
            slnBuilder.AppendLine("\tEndGlobalSection");

            // Output the project to solution folder mappings
            slnBuilder.AppendLine("\tGlobalSection(NestedProjects) = preSolution");
            foreach (var projectFolder in projectFolders)
            {
                foreach (var slnProject in projectFolder.Projects)
                {
                    slnBuilder.AppendLine($"\t\t{slnProject.ProjectGuid} = {projectFolder.ProjectGuid}");
                }
            }
            slnBuilder.AppendLine("\tEndGlobalSection");

            // Output the extensibility globals
            slnBuilder.AppendLine("\tGlobalSection(ExtensibilityGlobals) = postSolution");
            slnBuilder.AppendLine($"\t\tSolutionGuid = {solution.Guid}");
            slnBuilder.AppendLine("\tEndGlobalSection");

            slnBuilder.AppendLine("EndGlobal");

            File.WriteAllText(solutionPath, slnBuilder.ToString());
        }

        private static string GetNameForSolution(string path)
        {
            if (path.Length < 0)
                throw new ArgumentException("Invalid base bath for solution", nameof(path));

            if (path[path.Length - 1] == Path.DirectorySeparatorChar || path[path.Length - 1] == Path.AltDirectorySeparatorChar)
            {
                return GetNameForSolution(path.Substring(0, path.Length - 1));
            }
            return Path.GetFileName(path);           
        }

        internal class ProjectFolder
        {
            public string Name { get; }
            public string ProjectGuid { get; }
            public string SolutionGuid { get { return "{2150E333-8FDC-42A3-9474-1A3956D46DE8}"; } }
            public string ProjectFolderPath { get; }
            public bool InUse { get; set; }
            public List<ProjectFolder> DependsOn { get; set; } = new List<ProjectFolder>();

            public bool FolderExists { get; }

            public List<SolutionProject> Projects { get; }

            public ProjectFolder(string basePath, string relPath, string projectId, string projectExcludePattern, bool searchRecursively = false)
            {
                Name = relPath;
                ProjectGuid = projectId;
                ProjectFolderPath = Path.Combine(basePath, relPath);
                FolderExists = Directory.Exists(ProjectFolderPath);
                Projects = new List<SolutionProject>();

                if (FolderExists)
                {
                    SearchOption searchOption = searchRecursively ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    Regex excludePattern = string.IsNullOrEmpty(projectExcludePattern) ? null : new Regex(projectExcludePattern);
                    string primaryProjectPrefix = Path.Combine(ProjectFolderPath, GetNameForSolution(basePath) + "." + relPath);
                    foreach (string proj in Directory.EnumerateFiles(ProjectFolderPath, "*proj", searchOption).OrderBy(p => p))
                    {
                        if (excludePattern == null || !excludePattern.IsMatch(proj))
                        {
                            if (proj.StartsWith(primaryProjectPrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                // Always put the primary project first in the list
                                Projects.Insert(0, new SolutionProject(proj));
                            }
                            else
                            {
                                Projects.Add(new SolutionProject(proj));
                            }
                        }
                    }
                }
            }
        }

        internal class Solution
        {
            public string Path { get; }
            public string Guid { get; }

            public Solution(string path)
            {
                Path = path;
                Guid = ReadSolutionGuid(path);
            }

            private static string ReadSolutionGuid(string path)
            {
                string solutionGuid = null;
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadLines(path))
                    {
                        if (line.StartsWith("\t\tSolutionGuid = "))
                        {
                            solutionGuid = line.Substring("\t\tSolutionGuid = ".Length);
                            break;
                        }
                    }
                }

                if (solutionGuid == null)
                {
                    solutionGuid = System.Guid.NewGuid().ToString("B").ToUpper();
                }

                return solutionGuid;
            }
        }

        internal class SolutionProject
        {
            public string ProjectPath { get; }
            public string ProjectGuid { get; }
            public string[] Configurations { get; set; }

            public SolutionProject(string projectPath)
            {
                ProjectPath = projectPath;
                ProjectGuid = ReadProjectGuid(projectPath);
                string configurationProps = Path.Combine(Path.GetDirectoryName(projectPath), "Configurations.props");
                if (File.Exists(configurationProps))
                {
                    Configurations = GetConfigurationStrings(configurationProps, addSuffixes:false);
                }
                else
                {
                    Configurations = new string[0];
                }
            }

            public string GetBestConfiguration(string buildConfiguration)
            {
                //TODO: We should use the FindBestConfigutation logic from the build tasks
                var match = Configurations.FirstOrDefault(c => c == buildConfiguration);
                if (match != null)
                    return match;

                match = Configurations.FirstOrDefault(c => buildConfiguration.StartsWith(c));
                if (match != null)
                    return match;

                // Try again with netstandard if we didn't find the specific build match.
                buildConfiguration = "netstandard-Windows_NT";
                match = Configurations.FirstOrDefault(c => c == buildConfiguration);
                if (match != null)
                    return match;

                match = Configurations.FirstOrDefault(c => buildConfiguration.StartsWith(c));
                if (match != null)
                    return match;

                if (Configurations.Length > 0)
                    return Configurations[0];

                return string.Empty;
            }

            private static string ReadProjectGuid(string projectFile)
            {
                var project = ProjectRootElement.Open(projectFile);
                ProjectPropertyElement projectGuid = project.Properties.FirstOrDefault(p => p.Name == "ProjectGuid");

                if (projectGuid == null)
                {
                    return Guid.NewGuid().ToString("B").ToUpper();
                }

                return projectGuid.Value;
            }

            public string SolutionGuid
            {
                get
                {
                    //ProjectTypeGuids for different projects, pulled from the Visual Studio regkeys
                    //TODO: Clean up or map these to actual projects, this is fragile
                    string slnGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"; // Windows (C#) Managed/CPS
                    if (ProjectPath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                    {
                        slnGuid = "{778DAE3C-4631-46EA-AA77-85C1314464D9}"; //Windows (VB.NET) Managed/CPS
                    }
                    if (ProjectPath.Contains("TestNativeService")) //Windows (Visual C++)
                    {
                        slnGuid = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
                    }
                    if (ProjectPath.Contains("WebServer.csproj")) //Web Application
                    {
                        slnGuid = "{349C5851-65DF-11DA-9384-00065B846F21}";
                    }

                    return slnGuid;
                }
            }
        }
    }
}
