// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        [Fact]
        public void UpdateBehavesCorrectlyAsSourceDocumentEvolves()
        {
            // dev authors new resx with no corresponding xliff
            string resx =
@"<root>
  <data name=""Hello"">
    <value>Hello!</value>
  </data>
  <data name=""Goodbye"">
    <value>Goodbye!</value>
  </data>
  <data name=""Apple"">
   <value>Apple</value>
  </data>
</root>";

            string xliff =
@"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <group id=""test.resx"" />
      <trans-unit id=""Hello"">
        <source>Hello!</source>
        <target state=""new"">Hello!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye!</source>
        <target state=""new"">Goodbye!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""new"">Apple</target>
        <note />
      </trans-unit>
    </body>
  </file>
</xliff>";
            Assert.Equal(xliff, Update(xliff: "", resx: resx));

            // loc team translates
            string xliffAfterFirstTranslation =
 @"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <group id=""test.resx"" />
      <trans-unit id=""Hello"">
        <source>Hello!</source>
        <target state=""translated"">Bonjour!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye!</source>
        <target state=""translated"">Au revoir!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""translated"">Apple</target>
        <note />
      </trans-unit>
    </body>
  </file>
</xliff>";

            // dev makes some changes
            string resxAfterFirstModification =
@"<root>
  <data name=""HelloWorld"">
    <value>Hello World!</value>
  </data>
  <data name=""Goodbye"" xml:space=""preserve"">
    <value>Goodbye World!</value>
  </data>
  <data name=""Apple"">
    <value>Apple</value>
    <comment>This is referring to fruit.</comment>
  </data>
  <data name=""Banana"">
    <value>Banana</value>
  </data>
</root>";

            string xliffAfterApplyingResxModification =
 @"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <group id=""test.resx"" />
      <trans-unit id=""Goodbye"">
        <source>Goodbye World!</source>
        <target state=""needs-review-translation"">Au revoir!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""needs-review-translation"">Apple</target>
        <note>This is referring to fruit.</note>
      </trans-unit>
      <trans-unit id=""HelloWorld"">
        <source>Hello World!</source>
        <target state=""new"">Hello World!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Banana"">
        <source>Banana</source>
        <target state=""new"">Banana</target>
        <note />
      </trans-unit>
    </body>
  </file>
</xliff>";

            Assert.Equal(
                xliffAfterApplyingResxModification, 
                Update(xliff: xliffAfterFirstTranslation, resx: resxAfterFirstModification));
        }

        private static string Update(string xliff, string resx)
        {
            var xliffDocument = new XliffDocument();

            if (string.IsNullOrEmpty(xliff))
            {
                xliffDocument.LoadNew("test.resx", "fr");
            }
            else
            {
                xliffDocument.Load(new StringReader(xliff));
            }

            var resxDocument = new ResxDocument();
            resxDocument.Load(new StringReader(resx));
            xliffDocument.Update(resxDocument);

            var writer = new StringWriter();
            xliffDocument.Save(writer);
            return writer.ToString();
        }
    }
}
