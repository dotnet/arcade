// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Frameworks;
using System;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class PackageIndexTests
    {
        [Fact]
        public void IndexCacheConsidersModifiedTime()
        {
            string packageIndexFile = $"{nameof(IndexCacheConsidersModifiedTime)}.json";
            
            PackageIndex packageIndex = new PackageIndex();
            packageIndex.Packages.Add("MyPackage", new PackageInfo());
            Assert.Equal(1, packageIndex.Packages.Count);
            Assert.True(packageIndex.Packages.ContainsKey("MyPackage"));
            packageIndex.Save(packageIndexFile);

            DateTime originalModifiedTime = File.GetLastWriteTimeUtc(packageIndexFile);
            string[] packageIndexFiles = new[] { packageIndexFile };

            packageIndex = PackageIndex.Load(packageIndexFiles);
            Assert.Equal(1, packageIndex.Packages.Count);
            Assert.True(packageIndex.Packages.ContainsKey("MyPackage"));
            
            packageIndex = new PackageIndex();
            packageIndex.Packages.Add("MyPackage", new PackageInfo());
            packageIndex.Packages.Add("MyPackage2", new PackageInfo());
            Assert.Equal(2, packageIndex.Packages.Count);
            Assert.True(packageIndex.Packages.ContainsKey("MyPackage"));
            Assert.True(packageIndex.Packages.ContainsKey("MyPackage2"));
            packageIndex.Save(packageIndexFile);

            // force the same modified time, but should be different size
            File.SetLastWriteTimeUtc(packageIndexFile, originalModifiedTime);
            packageIndex = PackageIndex.Load(packageIndexFiles);
            Assert.Equal(2, packageIndex.Packages.Count);
            Assert.True(packageIndex.Packages.ContainsKey("MyPackage"));
            Assert.True(packageIndex.Packages.ContainsKey("MyPackage2"));

            // now change the content so that it has the same size, but different modified time
            long previousLength = new FileInfo(packageIndexFile).Length;
            packageIndex.Packages.Remove("MyPackage2");
            packageIndex.Packages.Add("MyPackage3", new PackageInfo());
            packageIndex.Save(packageIndexFile);
            Assert.Equal(previousLength, new FileInfo(packageIndexFile).Length);

            // ensure we have a different modified time
            File.SetLastWriteTimeUtc(packageIndexFile, new DateTime(originalModifiedTime.Ticks + 100));
            packageIndex = PackageIndex.Load(packageIndexFiles);
            Assert.Equal(2, packageIndex.Packages.Count);
            Assert.True(packageIndex.Packages.ContainsKey("MyPackage"));
            Assert.True(packageIndex.Packages.ContainsKey("MyPackage3"));
        }
    }
}
