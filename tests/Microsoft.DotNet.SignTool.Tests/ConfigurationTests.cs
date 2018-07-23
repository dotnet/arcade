// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.SignTool.Tests
{
    public class ConfigurationTests
    {
        private BatchSignInput Load(string json)
        {
            var task = new SignToolTask();

            using (var reader = new StringReader(json))
            using (var writer = new StringWriter())
            {
                var data = Configuration.TryReadConfigFile(task.Log, reader, @"q:\outputPath");

                Assert.True(string.IsNullOrEmpty(writer.ToString()));
                return data;
            }
        }

        [Fact]
        public void MissingExcludeSection()
        {
            var json = @"
{
""sign"": []
}";
            var data = Load(json);
            Assert.Empty(data.FileNames);
            Assert.Empty(data.ExternalFileNames);
        }

        [Fact]
        public void MsiContent()
        {
            var json = @"
{
""sign"": [
{
""certificate"": ""Microsoft402"",
""strongName"": null,
""values"": [
    ""test.msi""
]
}]
}";

            var data = Load(json);
            Assert.Equal(new[] { "test.msi" }, data.FileNames.Select(x => x.Name));
            Assert.Empty(data.ExternalFileNames);
        }
    }
}
