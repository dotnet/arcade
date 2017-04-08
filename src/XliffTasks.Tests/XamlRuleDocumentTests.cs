// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Xunit;

namespace XliffTasks.Tests
{
    public class XamlRuleTranslationTests
    {
        [Fact]
        public void BasicLoadAndTranslate()
        {
            string source =
@"<Rule Name=""MyRule"" DisplayName=""My rule display name"" PageTemplate=""generic"" Description=""My rule description"" xmlns=""http://schemas.microsoft.com/build/2009/properties"">
  <Rule.Categories>
    <Category Name=""MyCategory"" DisplayName=""My category display name"" />
  </Rule.Categories>
  <EnumProperty Name=""MyEnumProperty"" DisplayName=""My enum property display name"" Category=""MyCategory"" Description=""Specifies the source file will be copied to the output directory."">
    <EnumValue Name=""First"" DisplayName=""Do the first thing"" />
    <EnumValue Name=""Second"" DisplayName=""Do the second thing"" />
    <EnumValue Name=""Third"" DisplayName=""Do the third thing"" />
  </EnumProperty>
  <BoolProperty Name=""MyBoolProperty"" Description=""My bool property description."" />
</Rule>";

            var translations = new Dictionary<string, string>
            {
                ["Rule|MyRule|DisplayName"] = "AAA",
                ["Rule|MyRule|Description"] = "BBB",
                ["Category|MyCategory|DisplayName"] = "CCC",
                ["EnumProperty|MyEnumProperty|DisplayName"] = "DDD",
                ["EnumProperty|MyEnumProperty|Description"] = "EEE",
                ["EnumValue|MyEnumProperty.First|DisplayName"] = "FFF",
                ["EnumValue|MyEnumProperty.Second|DisplayName"] = "GGG",
                ["EnumValue|MyEnumProperty.Third|DisplayName"] = "HHH",
                ["BoolProperty|MyBoolProperty|Description"] = "III",
                
            };

            string expectedTranslation =
@"<Rule Name=""MyRule"" DisplayName=""AAA"" PageTemplate=""generic"" Description=""BBB"" xmlns=""http://schemas.microsoft.com/build/2009/properties"">
  <Rule.Categories>
    <Category Name=""MyCategory"" DisplayName=""CCC"" />
  </Rule.Categories>
  <EnumProperty Name=""MyEnumProperty"" DisplayName=""DDD"" Category=""MyCategory"" Description=""EEE"">
    <EnumValue Name=""First"" DisplayName=""FFF"" />
    <EnumValue Name=""Second"" DisplayName=""GGG"" />
    <EnumValue Name=""Third"" DisplayName=""HHH"" />
  </EnumProperty>
  <BoolProperty Name=""MyBoolProperty"" Description=""III"" />
</Rule>";

            var document = new XamlRuleDocument();
            var writer = new StringWriter();
            document.Load(new StringReader(source));
            document.Translate(translations);
            document.Save(writer);

            Assert.Equal(expectedTranslation, writer.ToString());
        }
    }
}
