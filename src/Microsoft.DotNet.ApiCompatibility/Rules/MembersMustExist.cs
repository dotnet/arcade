using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class MembersMustExist : Rule
    {
        public override void Run(TypeMapper mapper, List<CompatDifference> differences)
        {
            ITypeSymbol left = mapper.Left;
            if (left != null && mapper.Right == null)
                differences.Add(new CompatDifference(DiagnosticIds.TypeMustExist, $"Type '{left.ToDisplayString()}' exists on the contract but not on the implementation", DifferenceType.Removed, left));
        }

        public override void Run(MemberMapper mapper, List<CompatDifference> differences)
        {
            ISymbol left = mapper.Left;
            if (left != null && mapper.Right == null)
            {
                // Events and properties are handled via their accessors.
                if (left.Kind == SymbolKind.Property || left.Kind == SymbolKind.Event)
                    return;

                if (left is IMethodSymbol method)
                {
                    // Will be handled by a different rule
                    if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
                        return;

                    // TODO: handle overriden or methods promoted to a base type? 
                }

                differences.Add(new CompatDifference(DiagnosticIds.MemberMustExist, $"Member '{left.ToDisplayString()}' exists on the contract but not on the implementation", DifferenceType.Removed, left));
            }
        }
    }
}
