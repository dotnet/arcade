// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter
    {
        public void WriteNamespaceDeclaration(INamespaceDefinition ns)
        {
            WriteKeyword("namespace");
            WriteIdentifier(TypeHelper.GetNamespaceName((IUnitNamespace)ns, NameFormattingOptions.None));
        }
    }
}
