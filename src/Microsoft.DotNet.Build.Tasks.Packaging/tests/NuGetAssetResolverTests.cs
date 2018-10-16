// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Frameworks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class NuGetAssetResolverTests
    {
        private Log _log;


        public NuGetAssetResolverTests(ITestOutputHelper output)
        {
            _log = new Log(output);
        }

        [Fact]
        public void RuntimeResolutionTest()
        {
            string[] items =
            {
                "runtimes/any/lib/netcore50/System.Xml.XmlSerializer.dll",
                "runtimes/aot/lib/netcore50/_._"
            };

            NuGetAssetResolver resolver = new NuGetAssetResolver("runtime.json", items);

            var runtimeItems = resolver.GetRuntimeItems(NuGetFramework.Parse("netcore50"), "win10-x64-aot");

            Assert.NotNull(runtimeItems);
            Assert.Equal(1, runtimeItems.Items.Count);

            // Fails due to https://github.com/NuGet/Home/issues/1676
            // Assert.Equal(items[1], runtimeItems.Items.FirstOrDefault().Path);
        }
    }
}
