// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Xunit;

namespace XliffTasks.Tests
{
    public class XliffDocumentTests
    {
        [Fact]
        public void LoadNewInitializesNewDocumentWithCorrectContent()
        {
            var xliffDocument = new XliffDocument();
            xliffDocument.LoadNew("test.resx", "es");

            var writer = new StringWriter();
            xliffDocument.Save(writer);

            string expected =
@"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""es"" original=""test.resx"">
    <body>
      <group id=""test.resx"" />
    </body>
  </file>
</xliff>";

            Assert.Equal(expected, writer.ToString());
        }
    }
}
