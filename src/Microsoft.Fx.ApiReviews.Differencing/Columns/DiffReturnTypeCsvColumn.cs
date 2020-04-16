// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Mappings;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffReturnTypeCsvColumn : DiffCsvColumn
    {
        public DiffReturnTypeCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration)
        {
        }

        public override string Name
        {
            get { return "ReturnType"; }
        }

        public override string GetValue(MemberMapping mapping)
        {
            var signature = mapping.Representative as ISignature;
            return signature == null
                       ? null
                       : signature.Type.GetTypeName(NameFormattingOptions.DocumentationId);
        }
    }
}
