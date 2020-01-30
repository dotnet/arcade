// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework.Sdk
{
    public class GenerateRuntimeOSPropsFile : BuildTask
    {
        public const string RuntimeOSProperty = "RuntimeOS";

        [Required]
        public string RuntimePropsFilePath { get ; set; }

        public override bool Execute()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            CreateRuntimeIdentifier(project);
            project.Save(RuntimePropsFilePath);
            return !Log.HasLoggedErrors;
        }

        private static void CreateRuntimeIdentifier(ProjectRootElement project)
        {
            string rid = PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier();
            string[] ridParts = rid.Split('-');

            if (ridParts.Length < 1)
            {
                throw new System.InvalidOperationException($"Unknown rid format {rid}.");
            }

            string osNameAndVersion = ridParts[0];

            var propertyGroup = project.CreatePropertyGroupElement();
            project.AppendChild(propertyGroup);

            var runtimeProperty = propertyGroup.AddProperty(RuntimeOSProperty, osNameAndVersion);
            runtimeProperty.Condition = $"'$({RuntimeOSProperty})' == ''";

            Log.LogMessage($"Running on OS with RID '{rid}', so defaulting RuntimeOS to '{osNameAndVersion}'");
        }
    }
}
