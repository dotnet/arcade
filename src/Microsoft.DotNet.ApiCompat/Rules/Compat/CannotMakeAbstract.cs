// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class CannotMakeAbstract : CompatDifferenceRule
    {
        public override DifferenceType Diff(IDifferences differences, ITypeDefinitionMember impl, ITypeDefinitionMember contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (impl.IsAbstract() && !contract.IsAbstract())
            {
                differences.AddIncompatibleDifference("CannotMakeMemberAbstract",
                    $"Member '{impl.FullName()}' is abstract in the {Implementation} but is not abstract in the {Contract}.");

                return DifferenceType.Changed;
            }

            return DifferenceType.Unknown;
        }

        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (impl.IsAbstract && !contract.IsAbstract)
            {
                differences.AddIncompatibleDifference("CannotMakeTypeAbstract",
                    $"Type '{impl.FullName()}' is abstract in the {Implementation} but is not abstract in the {Contract}.");

                return DifferenceType.Changed;
            }

            return DifferenceType.Unknown;
        }
    }
}
