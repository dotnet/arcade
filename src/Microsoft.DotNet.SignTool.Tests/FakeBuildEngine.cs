// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.DotNet.SignTool.Tests
{
    class FakeBuildEngine : IBuildEngine
    {
        // It's just a test helper so public fields is fine.
        public List<BuildErrorEventArgs> LogErrorEvents = new List<BuildErrorEventArgs>();

        public List<BuildMessageEventArgs> LogMessageEvents =
            new List<BuildMessageEventArgs>();

        public List<CustomBuildEventArgs> LogCustomEvents =
            new List<CustomBuildEventArgs>();

        public List<BuildWarningEventArgs> LogWarningEvents =
            new List<BuildWarningEventArgs>();

        public readonly List<ImmutableArray<XElement>> FilesToSign = new List<ImmutableArray<XElement>>();

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            var xml = XDocument.Load(projectFileName);

            var itemGroupNode = xml.Descendants("ItemGroup");

            if (itemGroupNode.Count() == 0)
            {
                LogErrorEvent(new BuildErrorEventArgs(string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, "Didn't find a project node.", "", ""));
                return false;
            }
            else if (itemGroupNode.Count() > 1)
            {
                LogErrorEvent(new BuildErrorEventArgs(string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, "Only one <ItemGroup> is expected on this file.", "", ""));
                return false;
            }
            else
            {
                var filesToSign = itemGroupNode.Descendants("FilesToSign").ToImmutableArray();
                FilesToSign.Add(filesToSign);

                foreach (var file in filesToSign)
                {
                    FakeSignTool.SignFile(file.Attribute("Include").Value);
                }

                return true;
            }
        }

        public int ColumnNumberOfTaskNode
        {
            get { return 0; }
        }

        public bool ContinueOnError
        {
            get; set;
        }

        public int LineNumberOfTaskNode
        {
            get { return 0; }
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            LogCustomEvents.Add(e);
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            LogErrorEvents.Add(e);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            LogMessageEvents.Add(e);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            LogWarningEvents.Add(e);
        }

        public string ProjectFileOfTaskNode
        {
            get { return "fake ProjectFileOfTaskNode"; }
        }

    }
}
