// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Internal.Utilities;

public class DependencyDetail
{
    public DependencyDetail()
    {
        Locations = new List<string>();
    }

    public DependencyDetail(DependencyDetail other)
    {
        Name = other.Name;
        Version = other.Version;
        RepoUri = other.RepoUri;
        Commit = other.Commit;
        Pinned = other.Pinned;
        Type = other.Type;
        CoherentParentDependencyName = other.CoherentParentDependencyName;
        Locations = other.Locations;
    }

    public string Name { get; set; }

    /// <summary>
    ///     Version of dependency.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    ///     Source repository uri that the dependency was produced from.
    /// </summary>
    public string RepoUri { get; set; }

    /// <summary>
    ///     Source commit that the dependency was produced from.
    /// </summary>
    public string Commit { get; set; }

    /// <summary>
    ///     True if the dependency should not be updated, false otherwise.
    /// </summary>
    public bool Pinned { get; set; }

    /// <summary>
    ///     Type of dependency (e.g. Product or Toolset).
    /// </summary>
    public DependencyType Type { get; set; }

    /// <summary>
    ///     Another dependency for which this dependency must be coherent with.
    ///     This means:
    ///     If I have 3 repositories which have a potentially incoherent dependency structure:
    ///     A
    ///     |\
    ///     B |
    ///     \ |
    ///      C
    ///     A different version of C could appear in A and B.
    ///     This may not be a problem, or it could be undesirable.
    ///     This can be resolved to be always coherent by identifying that A's dependency on C
    ///     must be coherent with parent B. Specifically, this means that the build that produced B must
    ///     also have an input build that produced C.
    ///     
    ///     Concretely for .NET Core, core-setup has a dependency on Microsoft.Private.CoreFx.NETCoreApp produced
    ///     in corefx, and Microsoft.NETCore.Runtime.CoreCLR produced in coreclr.  corefx has a dependency on
    ///     Microsoft.NETCore.Runtime.CoreCLR. This means that when updating Microsoft.Private.CoreFx.NETCoreApp
    ///     in core-setup, also update Microsoft.NETCore.Runtime.CoreCLR to the version used to produce that
    ///     Microsoft.Private.CoreFx.NETCoreApp. By corrolary, that means Microsoft.NETCore.Runtime.CoreCLR cannot
    ///     be updated unless that version exists in the subtree of Microsoft.Private.CoreFx.NETCoreApp.
    ///     
    ///     Coherent parent dependencies are specified in Version.Details.xml as follows:
    ///     <![CDATA[
    ///         <Dependency Name="Microsoft.NETCore.App" Version="1.0.0-beta.19151.1" >
    ///             <Uri>https://github.com/dotnet/core-setup</Uri>
    ///             <Sha>abcd</Sha>
    ///         </Dependency>
    ///         <Dependency Name="Microsoft.Private.CoreFx.NETCoreApp" Version="1.2.3" CoherentParentDependency="Microsoft.NETCore.App">
    ///             <Uri>https://github.com/dotnet/corefx</Uri>
    ///             <Sha>defg</Sha>
    ///         </Dependency>
    ///      ]]>
    /// </summary>
    /// 
    public string CoherentParentDependencyName { get; set; }

    /// <summary>
    /// Asset locations for the dependency
    /// </summary>
    public IEnumerable<string> Locations { get; set; }

    /// <summary>
    /// Information whether dependency is needed for source-build.
    /// </summary>
    public SourceBuildInfo SourceBuild { get; set; }
}
