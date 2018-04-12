// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using XliffTasks.Model;
using Xunit;

namespace XliffTasks.Tests
{
    public class XlfDocumentTests
    {
        [Fact]
        public void LoadNewInitializesNewDocumentWithCorrectContent()
        {
            var xliffDocument = new XlfDocument();
            xliffDocument.LoadNew("es");

            var writer = new StringWriter();
            xliffDocument.Save(writer);

            string expected =
@"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""es"" original=""_"">
    <body />
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
    <comment>Greeting</comment>
  </data>
  <data name=""Goodbye"">
    <value>Goodbye!</value>
  </data>
  <data name=""Apple"">
   <value>Apple</value>
   <comment>Tasty</comment>
  </data>
</root>";

            string xliff =
@"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""new"">Apple</target>
        <note>Tasty</note>
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye!</source>
        <target state=""new"">Goodbye!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Hello"">
        <source>Hello!</source>
        <target state=""new"">Hello!</target>
        <note>Greeting</note>
      </trans-unit>
    </body>
  </file>
</xliff>";
             AssertEx.EqualIgnoringLineEndings(xliff, Update(xliff: "", resx: resx));

            // loc team translates
            string xliffAfterFirstTranslation =
 @"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""translated"">Apple</target>
        <note>Tasty</note>
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye!</source>
        <target state=""translated"">Au revoir!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Hello"">
        <source>Hello!</source>
        <target state=""translated"">Bonjour!</target>
        <note>Greeting</note>
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
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""needs-review-translation"">Apple</target>
        <note>This is referring to fruit.</note>
      </trans-unit>
      <trans-unit id=""Banana"">
        <source>Banana</source>
        <target state=""new"">Banana</target>
        <note />
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye World!</source>
        <target state=""needs-review-translation"">Au revoir!</target>
        <note />
      </trans-unit>
      <trans-unit id=""HelloWorld"">
        <source>Hello World!</source>
        <target state=""new"">Hello World!</target>
        <note />
      </trans-unit>
    </body>
  </file>
</xliff>";

             AssertEx.EqualIgnoringLineEndings(
                xliffAfterApplyingResxModification,
                Update(xliff: xliffAfterFirstTranslation, resx: resxAfterFirstModification));
        }

        [Fact]
        public void NewItemThatShouldBeLastEndsUpLast()
        {
            // Dev has just added "Zucchini" item to RESX
            string resx =
@"<root>
  <data name=""Hello"">
    <value>Hello!</value>
    <comment>Greeting</comment>
  </data>
  <data name=""Goodbye"">
    <value>Goodbye!</value>
  </data>
  <data name=""Apple"">
   <value>Apple</value>
   <comment>Tasty</comment>
  </data>
  <data name=""Zucchini"">
    <value>Zucchini</value>
    <comment>My children won't eat it.</comment>
  </data>
</root>";

            string xliffBeforeUpdate =
@"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""new"">Apple</target>
        <note>Tasty</note>
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye!</source>
        <target state=""new"">Goodbye!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Hello"">
        <source>Hello!</source>
        <target state=""new"">Hello!</target>
        <note>Greeting</note>
      </trans-unit>
    </body>
  </file>
</xliff>";

            string xliffAfterUpdate =
@"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""new"">Apple</target>
        <note>Tasty</note>
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye!</source>
        <target state=""new"">Goodbye!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Hello"">
        <source>Hello!</source>
        <target state=""new"">Hello!</target>
        <note>Greeting</note>
      </trans-unit>
      <trans-unit id=""Zucchini"">
        <source>Zucchini</source>
        <target state=""new"">Zucchini</target>
        <note>My children won't eat it.</note>
      </trans-unit>
    </body>
  </file>
</xliff>";

            AssertEx.EqualIgnoringLineEndings(
                xliffAfterUpdate,
                Update(xliff: xliffBeforeUpdate, resx: resx));

        }

        [Fact]
        public void CheckSorting()
        {
            string xliff =
@"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <trans-unit id=""Gamma"">
        <source>Hello!</source>
        <target state=""new"">Hello!</target>
        <note>Greeting</note>
      </trans-unit>
      <trans-unit id=""Beta"">
        <source>Goodbye!</source>
        <target state=""new"">Goodbye!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Alpha"">
        <source>Apple</source>
        <target state=""new"">Apple</target>
        <note>Tasty</note>
      </trans-unit>
    </body>
  </file>
</xliff>";

            string xliffAfterSorting =
@"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <trans-unit id=""Alpha"">
        <source>Apple</source>
        <target state=""new"">Apple</target>
        <note>Tasty</note>
      </trans-unit>
      <trans-unit id=""Beta"">
        <source>Goodbye!</source>
        <target state=""new"">Goodbye!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Gamma"">
        <source>Hello!</source>
        <target state=""new"">Hello!</target>
        <note>Greeting</note>
      </trans-unit>
    </body>
  </file>
</xliff>";

            AssertEx.EqualIgnoringLineEndings(
                xliffAfterSorting,
                Sort(xliff));
        }

        [Fact]
        public void UntranslatedResourceCount_Zero()
        {
            string xliff =
 @"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""translated"">Apple</target>
        <note>Tasty</note>
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye!</source>
        <target state=""translated"">Au revoir!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Hello"">
        <source>Hello!</source>
        <target state=""translated"">Bonjour!</target>
        <note>Greeting</note>
      </trans-unit>
    </body>
  </file>
</xliff>";

            Assert.Equal(expected: 0, actual: UntranslatedResourceCount(xliff));
        }

        [Fact]
        public void UntranslatedResourceCount_Two()
        {
            string xliff =
 @"<xliff xmlns=""urn:oasis:names:tc:xliff:document:1.2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" version=""1.2"" xsi:schemaLocation=""urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd"">
  <file datatype=""xml"" source-language=""en"" target-language=""fr"" original=""test.resx"">
    <body>
      <trans-unit id=""Apple"">
        <source>Apple</source>
        <target state=""translated"">Apple</target>
        <note>Tasty</note>
      </trans-unit>
      <trans-unit id=""Goodbye"">
        <source>Goodbye!</source>
        <target state=""new"">Goodbye!</target>
        <note />
      </trans-unit>
      <trans-unit id=""Hello"">
        <source>Hello!</source>
        <target state=""needs-review-translation"">Hallo!</target>
        <note>Greeting</note>
      </trans-unit>
    </body>
  </file>
</xliff>";

            Assert.Equal(expected: 2, actual: UntranslatedResourceCount(xliff));
        }

        private static string Sort(string xliff)
        {
            var xliffDocument = new XlfDocument();
            xliffDocument.Load(new StringReader(xliff));

            xliffDocument.Sort();

            var writer = new StringWriter();
            xliffDocument.Save(writer);
            return writer.ToString();
        }

        private static string Update(string xliff, string resx)
        {
            var xliffDocument = new XlfDocument();

            if (string.IsNullOrEmpty(xliff))
            {
                xliffDocument.LoadNew("fr");
            }
            else
            {
                xliffDocument.Load(new StringReader(xliff));
            }

            var resxDocument = new ResxDocument();
            resxDocument.Load(new StringReader(resx));
            xliffDocument.Update(resxDocument, "test.resx");

            var writer = new StringWriter();
            xliffDocument.Save(writer);
            return writer.ToString();
        }

        private static int UntranslatedResourceCount(string xliff)
        {
            var xliffDocument = new XlfDocument();
            xliffDocument.Load(new StringReader(xliff));

            return xliffDocument.GetUntranslatedResourceCount();
        }
    }
}
