// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using XliffTasks.Model;
using Xunit;

namespace XliffTasks.Tests
{
    public class ResxTranslationTests
    {
        [Fact]
        public void BasicLoadAndTranslate()
        {
            string source =
@"<root>
  <data name=""Hello"" xml:space=""preserve"">
    <value>Hello!</value>
  </data>
  <data name=""Goodbye"" xml:space=""preserve"">
    <value>Goodbye!</value>
  </data>
</root>";

            var translations = new Dictionary<string, string>
            {
                ["Hello"] = "Bonjour!",
                ["Goodbye"] = "Au revoir!",
            };

            string expectedTranslation =
@"<root>
  <data name=""Hello"" xml:space=""preserve"">
    <value>Bonjour!</value>
  </data>
  <data name=""Goodbye"" xml:space=""preserve"">
    <value>Au revoir!</value>
  </data>
</root>";

            var document = new ResxDocument();
            var writer = new StringWriter();
            document.Load(new StringReader(source));
            document.Translate(translations);
            document.Save(writer);

            AssertHelper.AssertWithoutLineEndingDifference(expectedTranslation, writer.ToString());
        }

        [Fact]
        public void RewriteFileReferenceToAbsoluteInDestinyFolder()
        {
            string source =
@"<root>
  <data name=""400"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>Resources\Package.ico;System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
  </data>
</root>";

            var translations = new Dictionary<string, string>
            {
                ["Hello"] = "Bonjour!",
            };

            string expectedTranslation =
@"<root>
  <data name=""400"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>E:\sourceFolder\Resources\Package.ico;System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
  </data>
</root>";

            var document = new ResxDocument();
            var writer = new StringWriter();
            document.Load(new StringReader(source));
            document.RewriteRelativePathsToAbsolute(
                @"E:\sourceFolder\Resources.resx");
            document.Save(writer);

            AssertHelper.AssertWithoutLineEndingDifference(expectedTranslation, writer.ToString());
        }

    }
}
