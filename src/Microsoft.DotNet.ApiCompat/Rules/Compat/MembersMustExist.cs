// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Mappings;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class MembersMustExist : CompatDifferenceRule
    {
        [Import]
        public IEqualityComparer<ITypeReference> _typeComparer { get; set; } = null;

        public enum FindMethodResult
        {
            Found,
            NotFound,
            ReturnTypeChanged
        }

        public override DifferenceType Diff(IDifferences differences, MemberMapping mapping)
        {
            ITypeDefinitionMember implMember = mapping[0];
            ITypeDefinitionMember contractMember = mapping[1];
            IMethodDefinition foundMethod;

            if (!(implMember == null && contractMember != null))
                return DifferenceType.Unknown;

            // Nested types are handled separately.
            // @TODO: Events and Properties - should we consider these too (or rely on the fact that dropping one of these will also drop their accessors.) 
            if (!(contractMember is IMethodDefinition || contractMember is IFieldDefinition))
                return DifferenceType.Unknown;

            string incompatibeDifferenceMessage = $"Member '{contractMember.FullName()}' does not exist in the {Implementation} but it does exist in the {Contract}.";

            ITypeDefinition contractType = mapping.ContainingType[0];
            if (contractType != null)
            {
                IMethodDefinition contractMethod = contractMember as IMethodDefinition;
                if (contractMethod != null)
                {
                    // If the contract is a Explicit Interface method, we don't need to check if the method is in implementation since that will be caught by different rule.
                    if (contractMethod.IsExplicitInterfaceMethod())
                        return DifferenceType.Unknown;



                    // It is valid to promote a member from a base type up so check to see if it member exits on a base type.
                    var lookForMethodInBaseResult = FindMatchingBase(contractType, contractMethod, out foundMethod);
                    if (lookForMethodInBaseResult == FindMethodResult.Found)
                        return DifferenceType.Unknown;
                    if (lookForMethodInBaseResult == FindMethodResult.ReturnTypeChanged)
                        incompatibeDifferenceMessage += $" There does exist a member with return type '{foundMethod.GetReturnType().FullName()}' instead of '{contractMethod.GetReturnType().FullName()}'";
                }
            }

            differences.AddIncompatibleDifference(this, incompatibeDifferenceMessage);

            return DifferenceType.Added;
        }

        private FindMethodResult FindMatchingBase(ITypeDefinition type, IMethodDefinition method, out IMethodDefinition resultMethod)
        {
            resultMethod = null;
            if (type == null || method.IsConstructor)
            {
                return FindMethodResult.NotFound;
            }

            bool foundMethodWithDifferentReturnType = false;

            foreach (var baseType in type.GetAllBaseTypes())
            {
                FindMethodResult found = FindMethodInCollection(method, baseType.Methods, out resultMethod, false);
                if (found == FindMethodResult.Found)
                    return found;
                if (found == FindMethodResult.ReturnTypeChanged)
                    foundMethodWithDifferentReturnType = true;
            }
            if (foundMethodWithDifferentReturnType)
                return FindMethodResult.ReturnTypeChanged;

            return FindMethodResult.NotFound;
        }

        private FindMethodResult FindMethodInCollection(IMethodDefinition targetMethod, IEnumerable<IMethodDefinition> collectionOfMethods, out IMethodDefinition foundMethod, bool removeExplicitName)
        {
            string targetMethodName = (removeExplicitName) ? targetMethod.GetNameWithoutExplicitType() : targetMethod.Name.Value;
            bool foundDifferentReturntype = false;
            foundMethod = null;
            foreach (IMethodDefinition potentialMatch in collectionOfMethods)
            {
                if (removeExplicitName && potentialMatch.IsExplicitInterfaceMethod()) continue;

                if (targetMethodName == potentialMatch.Name.Value)
                {
                    if (ParameterTypesAreEqual(potentialMatch, targetMethod))
                    {
                        if (!ReturnTypesMatch(potentialMatch, targetMethod))
                        {
                            foundDifferentReturntype = true;
                            foundMethod = potentialMatch;
                        }

                        if (!targetMethod.IsGeneric && !potentialMatch.IsGeneric)
                        {
                            foundMethod = potentialMatch;
                            return FindMethodResult.Found;
                        }

                        if (targetMethod.GenericParameterCount == potentialMatch.GenericParameterCount)
                        {
                            foundMethod = potentialMatch;
                            return FindMethodResult.Found;
                        }
                    }
                }
            }
            if (foundDifferentReturntype)
                return FindMethodResult.ReturnTypeChanged;
            return FindMethodResult.NotFound;
        }

        private bool ParameterTypesAreEqual(IMethodDefinition implMethod, IMethodDefinition contractMethod)
        {
            IParameterDefinition[] params1 = implMethod.Parameters.ToArray();
            IParameterDefinition[] params2 = contractMethod.Parameters.ToArray();

            if (params1.Length != params2.Length)
                return false;

            for (int i = 0; i < params1.Length; i++)
            {
                IParameterDefinition param1 = params1[i];
                IParameterDefinition param2 = params2[i];

                if (!_typeComparer.Equals(param1.Type, param2.Type))
                    return false;
            }

            return true;
        }

        public bool ReturnTypesMatch(IMethodDefinition implMethod, IMethodDefinition contractMethod)
        {
            ITypeReference implType = implMethod.GetReturnType();
            ITypeReference contractType = contractMethod.GetReturnType();

            if (implType == null || contractType == null)
                return true;

            if (!_typeComparer.Equals(implType, contractType))
            {
                return false;
            }

            return true;
        }
    }
}
