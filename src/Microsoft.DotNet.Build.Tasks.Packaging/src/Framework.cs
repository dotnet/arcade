// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class FrameworkSet
    {
        private const string LastNonSemanticVersionsFileName = "LastNonSemanticVersions.xml";

        // avoid parsing the same documents multiple times on a single node.
        private static Dictionary<string, FrameworkSet> s_frameworkSetCache = new Dictionary<string, FrameworkSet>();
        private static object s_frameworkSetCacheLock = new object();

        public FrameworkSet()
        {
            Frameworks = new Dictionary<string, SortedSet<Framework>>();
            LastNonSemanticVersions = new Dictionary<string, Version>();
        }

        public static FrameworkSet Load(string frameworkListsPath)
        {
            FrameworkSet result;
            if (s_frameworkSetCache.TryGetValue(frameworkListsPath, out result))
                return result;

            result = new FrameworkSet();

            foreach (string fxDir in Directory.EnumerateDirectories(frameworkListsPath))
            {
                string targetName = Path.GetFileName(fxDir);
                Framework framework = new Framework(targetName);
                foreach (string frameworkListPath in Directory.EnumerateFiles(fxDir, "*.xml"))
                {
                    AddAssembliesFromFrameworkList(framework.Assemblies, frameworkListPath);
                }

                SortedSet<Framework> frameworkVersions = null;
                string fxId = framework.FrameworkName.Identifier;

                if (fxId == FrameworkConstants.FrameworkIdentifiers.Portable)
                {
                    // portable doesn't have version relationships, use the entire TFM
                    fxId = framework.FrameworkName.ToString();
                }

                if (!result.Frameworks.TryGetValue(fxId, out frameworkVersions))
                {
                    frameworkVersions = new SortedSet<Framework>();
                }

                frameworkVersions.Add(framework);

                result.Frameworks[fxId] = frameworkVersions;
            }

            string lastNonSemanticVersionsListPath = Path.Combine(frameworkListsPath, LastNonSemanticVersionsFileName);
            AddAssembliesFromFrameworkList(result.LastNonSemanticVersions, lastNonSemanticVersionsListPath);

            lock (s_frameworkSetCacheLock)
            {
                s_frameworkSetCache[frameworkListsPath] = result;
            }
            return result;
        }

        private static void AddAssembliesFromFrameworkList(IDictionary<string, Version> assemblies, string frameworkListPath)
        {
            XDocument frameworkList = XDocument.Load(frameworkListPath);
            foreach (var file in frameworkList.Element("FileList").Elements("File"))
            {
                string assemblyName = file.Attribute("AssemblyName").Value;
                var versionAttribute = file.Attribute("Version");
                Version supportedVersion = null;

                if (versionAttribute != null)
                {
                    supportedVersion = new Version(versionAttribute.Value);
                }

                // Use a file entry with no version to indicate any version, 
                // this is how Xamarin wishes us to support them
                assemblies.Add(assemblyName,
                    supportedVersion ??
                    new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
            }
        }

        public Dictionary<string, SortedSet<Framework>> Frameworks { get; private set; }

        public Dictionary<string, Version> LastNonSemanticVersions { get; private set; }
        
        /// <summary>
        /// Determines the significant API version given an assembly version.
        /// </summary>
        /// <param name="assemblyName">Name of assembly</param>
        /// <param name="assemblyVersion">Version of assembly</param>
        /// <returns>Lowest version with the same API surface as assemblyVersion</returns>
        public Version GetApiVersion(string assemblyName, Version assemblyVersion)
        {
            if (assemblyVersion == null)
            {
                return null;
            }

            if (assemblyVersion.Build == 0 && assemblyVersion.Revision == 0)
            {
                // fast path for X.Y.0.0
                return assemblyVersion;
            }

            Version latestLegacyVersion = null;
            LastNonSemanticVersions.TryGetValue(assemblyName, out latestLegacyVersion);

            if (latestLegacyVersion == null)
            {
                return new Version(assemblyVersion.Major, assemblyVersion.Minor, 0, 0);
            }
            else if (assemblyVersion.Major <= latestLegacyVersion.Major && assemblyVersion.Minor <= latestLegacyVersion.Minor)
            {
                // legacy version round build to nearest 10
                return new Version(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build - assemblyVersion.Build % 10, 0);
            }
            else
            {
                // new version
                return new Version(assemblyVersion.Major, assemblyVersion.Minor, 0, 0);
            }
        }
    }

    public class Framework : IComparable<Framework>
    {
        public Framework(string targetName)
        {
            Assemblies = new Dictionary<string, Version>();
            FrameworkName = new FrameworkName(targetName);
            var nugetFramework = new NuGetFramework(FrameworkName.Identifier, FrameworkName.Version, FrameworkName.Profile);
            ShortName = nugetFramework.GetShortFolderName();

            if (ShortName.EndsWith(nugetFramework.Version.Major.ToString()) && nugetFramework.Version.Minor == 0)
            {
                // prefer a trailing zero
                ShortName += "0";
            }

            if (ShortName == "win" || ShortName == "netcore45")
            {
                // prefer the versioned short name
                ShortName = "win8";
            }

            if (ShortName == "netcore451")
            {
                ShortName = "win81";
            }
        }

        public IDictionary<string, Version> Assemblies { get; private set; }
        public FrameworkName FrameworkName { get; private set; }
        public string ShortName { get; private set; }


        public int CompareTo(Framework other)
        {
            if (this.FrameworkName.Identifier != other.FrameworkName.Identifier)
            {
                throw new ArgumentException("Frameworks with different IDs are not comparable.", "other");
            }

            return this.FrameworkName.Version.CompareTo(other.FrameworkName.Version);
        }
    }
    public class Frameworks
    {
        private static FrameworkSet s_inboxFrameworks;
        private static FrameworkSet GetInboxFrameworks(string frameworkListsPath)
        {
            if (s_inboxFrameworks == null)
                s_inboxFrameworks = FrameworkSet.Load(frameworkListsPath);
            return s_inboxFrameworks;
        }
        public static string[] GetInboxFrameworksList(string frameworkListsPath, string assemblyName, string assemblyVersion, ILog log)
        {
            // if no version is specified just use 0.0.0.0 to evaluate for any version of the contract
            Version version = String.IsNullOrEmpty(assemblyVersion) ? new Version(0, 0, 0, 0) : new Version(assemblyVersion);

            FrameworkSet fxs = GetInboxFrameworks(frameworkListsPath);

            Version latestLegacyVersion = null;
            fxs.LastNonSemanticVersions.TryGetValue(assemblyName, out latestLegacyVersion);

            List<string> inboxIds = new List<string>();

            foreach (var fxVersions in fxs.Frameworks.Values)
            {
                // find the first version (if any) that supports this contract
                foreach (var fxVersion in fxVersions)
                {
                    Version supportedVersion;
                    if (fxVersion.Assemblies.TryGetValue(assemblyName, out supportedVersion))
                    {
                        if (supportedVersion >= version)
                        {
                            if (log != null)
                                log.LogMessage(LogImportance.Low, "inbox on {0}", fxVersion.ShortName);
                            inboxIds.Add(fxVersion.ShortName);
                            break;
                        }

                        // new versions represent API surface via major.minor only, so consider
                        // a contract as supported so long as the latest legacy version is supported
                        // and this contract's major.minor match the latest legacy version.
                        if (supportedVersion == latestLegacyVersion &&
                            version.Major == latestLegacyVersion.Major && version.Minor == latestLegacyVersion.Minor)
                        {
                            if (log != null)
                                log.LogMessage(LogImportance.Low, "Considering {0},Version={1} inbox on {2}, since it only differs in revsion.build from {3}", assemblyName, assemblyVersion, fxVersion.ShortName, latestLegacyVersion);
                            inboxIds.Add(fxVersion.ShortName);
                            break;
                        }
                    }
                }
            }
            return inboxIds.ToArray();
        }

        public static bool IsInbox(string frameworkListsPath, string framework, string assemblyName, string assemblyVersion)
        {
            NuGetFramework fx = NuGetFramework.Parse(framework);
            return IsInbox(frameworkListsPath, fx, assemblyName, assemblyVersion);
        }

        public static bool IsInbox(string frameworkListsPath, NuGetFramework framework, string assemblyName, string assemblyVersion)
        {
            if (framework.Framework == FrameworkConstants.FrameworkIdentifiers.UAP || 
                (framework.Framework == FrameworkConstants.FrameworkIdentifiers.NetCore && framework.Version >= FrameworkConstants.CommonFrameworks.NetCore50.Version))
            {
                // UAP & netcore50 or higher are completely OOB, despite being compatible with netcore4x which has inbox assemblies
                return false;
            }

            // if no version is specified just use 0.0.0.0 to evaluate for any version of the contract
            Version version = FrameworkUtilities.Ensure4PartVersion(assemblyVersion);
            FrameworkSet fxs = GetInboxFrameworks(frameworkListsPath);

            Version latestLegacyVersion = null;
            fxs.LastNonSemanticVersions.TryGetValue(assemblyName, out latestLegacyVersion);

            foreach (var fxVersions in fxs.Frameworks.Values)
            {
                // Get the nearest compatible framework from this set of frameworks.
                var nearest = FrameworkUtilities.GetNearest(framework, fxVersions.Select(fx => NuGetFramework.Parse(fx.ShortName)).ToArray());
                // If there are not compatible frameworks in the current framework set, there is not going to be a match.
                if (nearest == null)
                {
                    continue;
                }
                
                // don't allow PCL to specify inbox for non-PCL framework.
                if (nearest.IsPCL != framework.IsPCL)
                {
                    continue;
                }
                
                // find the first version (if any) that supports this contract
                foreach (var fxVersion in fxVersions)
                {
                    Version supportedVersion;
                    if (fxVersion.Assemblies.TryGetValue(assemblyName, out supportedVersion))
                    {
                        if (supportedVersion >= version)
                        {
                            return true;
                        }

                        // new versions represent API surface via major.minor only, so consider
                        // a contract as supported so long as the latest legacy version is supported
                        // and this contract's major.minor match the latest legacy version.
                        if (supportedVersion == latestLegacyVersion &&
                            version.Major == latestLegacyVersion.Major && version.Minor == latestLegacyVersion.Minor)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        internal static IEnumerable<NuGetFramework> GetAlllInboxFrameworks(string frameworkListsPath)
        {
            FrameworkSet fxs = FrameworkSet.Load(frameworkListsPath);
            return fxs.Frameworks.SelectMany(fxList => fxList.Value).Select(fx => NuGetFramework.Parse(fx.ShortName));
        }
    }
}
