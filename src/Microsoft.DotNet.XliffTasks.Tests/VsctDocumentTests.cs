// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using XliffTasks.Model;
using Xunit;

namespace XliffTasks.Tests
{
    public class VsctTranslationTests
    {
        [Fact]
        public void BasicLoadAndTranslate()
        {
            string source =
@"<CommandTable xmlns=""http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <Commands package=""guidTestPackage"">
    <Menus>
      <Menu id=""menuidOne"">
        <Strings>
          <MenuText>Some menu text</MenuText>
          <ButtonText>Some button text</ButtonText>
        </Strings>
      </Menu>
      <Menu id=""menuidTwo"">
        <Strings>
          <MenuText>More menu text</MenuText>
          <ButtonText>More button text</ButtonText>
        </Strings>
      </Menu>
    </Menus>
  </Commands>
</CommandTable>";

            Dictionary<string, string> translations = new()
            {
                ["menuidOne|MenuText"] = "Texte de menu",
                ["menuidOne|ButtonText"] = "Texte de button",
                ["menuidTwo|MenuText"] = "Plus de texte de menu",
                ["menuidTwo|ButtonText"] = "Plus de texte de button",
            };

            string expectedTranslation =
@"<CommandTable xmlns=""http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <Commands package=""guidTestPackage"">
    <Menus>
      <Menu id=""menuidOne"">
        <Strings>
          <MenuText>Texte de menu</MenuText>
          <ButtonText>Texte de button</ButtonText>
        </Strings>
      </Menu>
      <Menu id=""menuidTwo"">
        <Strings>
          <MenuText>Plus de texte de menu</MenuText>
          <ButtonText>Plus de texte de button</ButtonText>
        </Strings>
      </Menu>
    </Menus>
  </Commands>
</CommandTable>";

            VsctDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.Translate(translations);
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());
        }

        [Fact]
        public void RewriteHrefOfImageToRelativePathFromOutputFolder()
        {
            // Simulates: source .vsct is in src/MyProject/, translated output is in artifacts/obj/MyProject.xlf/
            string tempBase = Path.Combine(Path.GetTempPath(), "xliff-vsct-test-" + Path.GetRandomFileName());
            string sourceFolder = Path.Combine(tempBase, "src", "MyProject");
            string outputFolder = Path.Combine(tempBase, "artifacts", "obj", "MyProject.xlf");
            string sourceFullPath = Path.Combine(sourceFolder, "MyPackage.vsct");
            string outputFullPath = Path.Combine(outputFolder, "MyPackage.vsct");

            string resourceRelativePath = Path.Combine("Resources", "Images.png");
            string resourceAbsolutePath = Path.GetFullPath(Path.Combine(sourceFolder, resourceRelativePath));

            Uri fromUri = new Uri(outputFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri toUri = new Uri(resourceAbsolutePath);
            string expectedRelativePath = Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);

            string source =
$@"<CommandTable xmlns=""http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <Bitmaps>
    <Bitmap guid=""guidImages"" href=""{resourceRelativePath}"" usedList=""bmpPic1, bmpPic2, bmpPicSearch, bmpPicX, bmpPicArrows"" />
  </Bitmaps>
</CommandTable>";

            string expectedTranslation =
$@"<CommandTable xmlns=""http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <Bitmaps>
    <Bitmap guid=""guidImages"" href=""{expectedRelativePath}"" usedList=""bmpPic1, bmpPic2, bmpPicSearch, bmpPicX, bmpPicArrows"" />
  </Bitmaps>
</CommandTable>";

            VsctDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.RewriteRelativePathsForOutputPath(sourceFullPath, outputFullPath);
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());

            // The path in the output must NOT be absolute
            Assert.DoesNotContain(tempBase, writer.ToString());
        }

        [Fact]
        public void DoesNotNullReferenceWhenNoHRef()
        {
            string source =
@"<CommandTable xmlns=""http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <Bitmaps>
    <Bitmap guid=""guidImages"" usedList=""bmpPic1, bmpPic2, bmpPicSearch, bmpPicX, bmpPicArrows"" />
  </Bitmaps>
</CommandTable>";

            string sourceFullPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources.vsct");

            VsctDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.RewriteRelativePathsForOutputPath(sourceFullPath, sourceFullPath);
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(source, writer.ToString());
        }

        [Fact]
        public void NonUniqueIds()
        {
            string source =
@"<CommandTable xmlns=""http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <Commands package=""guidTestPackage"">
    <Menus>
      <Menu guid=""firstGuid"" id=""menuid"">
        <Strings>
          <MenuText>Some menu text</MenuText>
          <ButtonText>Some button text</ButtonText>
        </Strings>
      </Menu>
      <Menu guid=""secondGuid"" id=""menuid"">
        <Strings>
          <MenuText>More menu text</MenuText>
          <ButtonText>More button text</ButtonText>
        </Strings>
      </Menu>
      <Menu guid=""thirdGuid"" id=""otherMenuId"">
        <Strings>
          <MenuText>Even more menu text</MenuText>
          <ButtonText>Even more button text</ButtonText>
        </Strings>
      </Menu>
    </Menus>
  </Commands>
</CommandTable>";

            Dictionary<string, string> translations = new()
            {
                ["firstGuid|menuid|MenuText"] = "Texte du menu",
                ["firstGuid|menuid|ButtonText"] = "Texte du bouton",
                ["secondGuid|menuid|MenuText"] = "Plus de texte de menu",
                ["secondGuid|menuid|ButtonText"] = "Plus de texte de bouton",
                ["otherMenuId|MenuText"] = "Encore plus de texte de menu",
                ["otherMenuId|ButtonText"] = "Encore plus de texte de bouton",
            };

            string expectedTranslation =
@"<CommandTable xmlns=""http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <Commands package=""guidTestPackage"">
    <Menus>
      <Menu guid=""firstGuid"" id=""menuid"">
        <Strings>
          <MenuText>Texte du menu</MenuText>
          <ButtonText>Texte du bouton</ButtonText>
        </Strings>
      </Menu>
      <Menu guid=""secondGuid"" id=""menuid"">
        <Strings>
          <MenuText>Plus de texte de menu</MenuText>
          <ButtonText>Plus de texte de bouton</ButtonText>
        </Strings>
      </Menu>
      <Menu guid=""thirdGuid"" id=""otherMenuId"">
        <Strings>
          <MenuText>Encore plus de texte de menu</MenuText>
          <ButtonText>Encore plus de texte de bouton</ButtonText>
        </Strings>
      </Menu>
    </Menus>
  </Commands>
</CommandTable>";

            VsctDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.Translate(translations);
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());
        }
    }
}
