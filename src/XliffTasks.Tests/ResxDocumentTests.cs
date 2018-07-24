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

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());
        }

        [Fact]
        public void RewriteFileReferenceToAbsoluteInDestinyFolder()
        {
            string sourceFolder = Directory.GetCurrentDirectory();
            string expectedAbsoluteLocation = Path.Combine(
              Directory.GetCurrentDirectory(),
              @"Resources\Package.ico".Replace('\\', Path.DirectorySeparatorChar));
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
    <value>ABSOLUTEPATH;System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
  </data>
</root>".Replace("ABSOLUTEPATH", expectedAbsoluteLocation);

            var document = new ResxDocument();
            var writer = new StringWriter();
            document.Load(new StringReader(source));
            document.RewriteRelativePathsToAbsolute(
                Path.Combine(sourceFolder, "Resources.resx"));
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());
        }

        [Fact]
        public void GetReplacementCount_NoPlaceHolders()
        {
            var text = "Alpha";

            var maxPlaceHolderCount = ResxDocument.GetReplacementCount(text);

            Assert.Equal(expected: 0, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_OneSimplePlaceHolder()
        {
            var text = "{0}";
            var maxPlaceHolderCount = ResxDocument.GetReplacementCount(text);

            Assert.Equal(expected: 1, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_PlaceHolderWithAlignment()
        {
            var text = "{0,-3}";
            var maxPlaceHolderCount = ResxDocument.GetReplacementCount(text);

            Assert.Equal(expected: 1, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_PlaceHolderWithFormatString()
        {
            var text = "{0:N}";
            var maxPlaceHolderCount = ResxDocument.GetReplacementCount(text);

            Assert.Equal(expected: 1, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_WithAlignmentAndFormatString()
        {
            var text = "{0,-10:G1}";
            var maxPlaceHolderCount = ResxDocument.GetReplacementCount(text);

            Assert.Equal(expected: 1, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_MultiplePlaceHolders()
        {
            var text = "{0} {1}";
            var maxPlaceHolderCount = ResxDocument.GetReplacementCount(text);

            Assert.Equal(expected: 2, actual: maxPlaceHolderCount);
        }

        [Fact]
        public void GetReplacementCount_MultipleIdenticalPlaceHolders()
        {
            var text = "{2} {2}";
            var maxPlaceHolderCount = ResxDocument.GetReplacementCount(text);

            Assert.Equal(expected: 3, actual: maxPlaceHolderCount);
        }
    }
}
