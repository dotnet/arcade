// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Differs.Rules
{
    // Removed because it appears the *MustExist rules already supersede these.
    [ExportDifferenceRule]
    internal class CannotMakeMoreVisible : CompatDifferenceRule
    {
        public override DifferenceType Diff(IDifferences differences, ITypeDefinitionMember impl, ITypeDefinitionMember contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (HasReducedVisibility(contract.Visibility, impl.Visibility))
            {
                differences.AddIncompatibleDifference(this,
                    $"Visibility of member '{impl.FullName()}' is '{impl.GetVisibilityName()}' in the {Implementation} but '{contract.GetVisibilityName()}' in the {Contract}.");
                return DifferenceType.Changed;
            }

            return DifferenceType.Unknown;
        }

        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (HasReducedVisibility(contract.GetVisibility(), impl.GetVisibility()))
            {
                differences.AddIncompatibleDifference(this,
                    $"Visibility of type '{impl.FullName()}' is '{impl.GetVisibilityName()}' in the {Implementation} but '{contract.GetVisibilityName()}' in the {Contract}.");
                return DifferenceType.Changed;
            }

            return DifferenceType.Unknown;
        }

        private bool HasReducedVisibility(TypeMemberVisibility contract, TypeMemberVisibility implementation)
        {
            if (contract == implementation)
            {
                return false;
            }

            switch (implementation)
            {
                case TypeMemberVisibility.Public:
                    // If implementation is public then contract can be any visibility.
                    return false;
                case TypeMemberVisibility.FamilyOrAssembly:
                    // protected internal is an upgrade from everything but public.
                    return contract == TypeMemberVisibility.Public;
                case TypeMemberVisibility.Assembly: // internal
                case TypeMemberVisibility.Family: // protected
                    // internal and protected are upgrades only from private or private protected.
                    return contract != TypeMemberVisibility.Private && contract != TypeMemberVisibility.FamilyAndAssembly;
                case TypeMemberVisibility.FamilyAndAssembly:
                    // private protected is very restrictive; only an upgrade from private.
                    return contract != TypeMemberVisibility.Private;
                case TypeMemberVisibility.Private:
                    // private in the implementation is always a downgrade.
                    return true;
            }

            return false;
        }
    }
}
