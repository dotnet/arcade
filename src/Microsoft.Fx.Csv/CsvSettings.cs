// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.Fx.Csv
{
    public struct CsvSettings
    {
        public static CsvSettings Default = new CsvSettings(
                    encoding: Encoding.UTF8,
                    delimiter: ',',
                    textQualifier: '"'
                );

        public CsvSettings(Encoding encoding, char delimiter, char textQualifier)
            : this()
        {
            Encoding = encoding;
            Delimiter = delimiter;
            TextQualifier = textQualifier;
        }

        public Encoding Encoding { get; private set; }
        public char Delimiter { get; private set; }
        public char TextQualifier { get; private set; }

        public bool IsValid { get { return Encoding != null; } }
    }
}
