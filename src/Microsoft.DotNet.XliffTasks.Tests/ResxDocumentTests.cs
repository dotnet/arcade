// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

            Dictionary<string, string> translations = new()
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

            ResxDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.Translate(translations);
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());
        }

        [Fact]
        public void RewriteFileReferenceToRelativePathFromOutputFolder()
        {
            // Simulates: source .resx is in src/MyProject/, translated output .resx is in artifacts/obj/MyProject.xlf/
            // The resource file at src/MyProject/Resources/Package.ico should be referenced as
            // a relative path from the output directory.
            string tempBase = Path.Combine(Path.GetTempPath(), "xliff-test-" + Path.GetRandomFileName());
            string sourceFolder = Path.Combine(tempBase, "src", "MyProject");
            string outputFolder = Path.Combine(tempBase, "artifacts", "obj", "MyProject.xlf");
            string sourceFullPath = Path.Combine(sourceFolder, "Resources.resx");
            string outputFullPath = Path.Combine(outputFolder, "MyProject.resx");

            string resourceRelativePath = Path.Combine("Resources", "Package.ico");
            string resourceAbsolutePath = Path.GetFullPath(Path.Combine(sourceFolder, resourceRelativePath));

            // Compute expected relative path from output directory to resource file
            Uri fromUri = new Uri(outputFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri toUri = new Uri(resourceAbsolutePath);
            string expectedRelativePath = Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);

            string source =
$@"<root>
  <data name=""400"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>{resourceRelativePath};System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
  </data>
</root>";

            string expectedTranslation =
$@"<root>
  <data name=""400"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>{expectedRelativePath};System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
  </data>
</root>";

            ResxDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.RewriteRelativePathsForOutputPath(sourceFullPath, outputFullPath);
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());

            // The path in the output must NOT be absolute — it must be relative,
            // so the translated .resx remains valid after the repo is renamed or moved.
            Assert.DoesNotContain(tempBase, writer.ToString());
        }

        
    }
}
