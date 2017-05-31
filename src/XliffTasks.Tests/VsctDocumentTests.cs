// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

            var translations = new Dictionary<string, string>
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

            var document = new VsctDocument();
            var writer = new StringWriter();
            document.Load(new StringReader(source));
            document.Translate(translations);
            document.Save(writer);

            Assert.Equal(expectedTranslation, writer.ToString());
        }
    }
}
