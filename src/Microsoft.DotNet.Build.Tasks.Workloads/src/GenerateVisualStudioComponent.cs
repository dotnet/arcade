// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Generates the SWIX authoring for a component or component group.
    /// </summary>
    public class GenerateVisualStudioComponent : Task
    {
        /// <summary>
        /// The base path where the project source will be generated.
        /// </summary>
        [Required]
        public string IntermediateOutputBase
        {
            get;
            set;
        }

        internal string IntermediateOutputPath => Path.Combine(IntermediateOutputBase, ComponentName);

        /// <summary>
        /// When <see langword="true"/>, the component is visible in the individual components tab.
        /// </summary>
        public bool IsUiGroup
        {
            get;
            set;
        } = true;

        /// <summary>
        /// The name (ID) of the component.
        /// </summary>
        [Required]
        public string ComponentName
        {
            get;
            set;
        }

        /// <summary>
        /// The description of the component inside the Visual Studio setup UI. The description is
        /// displayed as a tooltip.
        /// </summary>
        public string ComponentDescription
        {
            get;
            set;
        }

        /// <summary>
        /// The title for the component to display inside the Visual Studio setup UI.
        /// </summary>
        public string ComponentTitle
        {
            get;
            set;
        }

        /// <summary>
        /// The component category. This value defines the heading in VS where the component will be listed in
        /// the individual components tab.
        /// </summary>
        public string ComponentCategory
        {
            get;
            set;
        }

        /// <summary>
        /// The component dependencies.
        /// </summary>
        [Required]
        public ITaskItem[] Dependencies
        {
            get;
            set;
        }

        /// <summary>
        /// The path of the generated .swixproj 
        /// </summary>
        [Output]
        public string GeneratedSwixProject
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the component.
        /// </summary>
        [Required]
        public string Version
        {
            get;
            set;
        }

        public override bool Execute()
        {
            StreamWriter swrWriter = null;

            try
            {
                string componentSwr = EmbeddedTemplates.Extract("component.swr", IntermediateOutputPath);
                string componentResSwr = EmbeddedTemplates.Extract("component.res.swr", IntermediateOutputPath);
                GeneratedSwixProject = EmbeddedTemplates.Extract("component.swixproj", IntermediateOutputPath, ComponentName + ".swixproj");

                Utils.StringReplace(componentSwr, GetReplacementTokens(), Encoding.UTF8);
                Utils.StringReplace(componentResSwr, GetReplacementTokens(), Encoding.UTF8);

                swrWriter = File.AppendText(componentSwr);

                foreach (ITaskItem package in Dependencies)
                {
                    AddDependency(swrWriter, package.ItemSpec, package.GetMetadata("Version"));
                }                

            }
            catch (Exception e)
            {
                Log.LogMessage(e.StackTrace);
                Log.LogErrorFromException(e);
            }
            finally
            {
                swrWriter?.WriteLine();
                swrWriter?.Flush();
                swrWriter?.Close();
            }

            return !Log.HasLoggedErrors;
        }

        private void AddDependency(StreamWriter writer, string packageName, string packageVersion)
        {
            // Indentation is critcal and should be done as follows
            //
            // vs.dependencies
            //   vs.dependency id=<packageID>
            //                 version=[1.2.3.4]
            
            writer.WriteLine($"  vs.dependency id={packageName}");
            writer.WriteLine($"                version=[{packageVersion}]");
        }

        private Dictionary<string, string> GetReplacementTokens()
        {
            return new Dictionary<string, string>()
            {
                {"__VS_PACKAGE_NAME__", ComponentName },
                {"__VS_PACKAGE_VERSION__", Version },
                {"__VS_IS_UI_GROUP__", IsUiGroup ? "yes" : "no" },
                {"__VS_COMPONENT_TITLE__", ComponentTitle },
                {"__VS_COMPONENT_DESCRIPTION__", ComponentDescription },
                {"__VS_COMPONENT_CATEGORY__", ComponentCategory }
            };
        }
    }
}
