// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Writers.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter
    {
        private void WriteGenericParameters(IEnumerable<IGenericParameter> genericParameters)
        {
            if (!genericParameters.Any())
                return;

            WriteSymbol("<");
            _writer.WriteList(genericParameters, p => WriteGenericParameter(p));
            WriteSymbol(">");
        }

        private void WriteGenericParameter(IGenericParameter param)
        {
            switch (param.Variance)
            {
                case TypeParameterVariance.Contravariant:
                    WriteKeyword("in"); break;
                case TypeParameterVariance.Covariant:
                    WriteKeyword("out"); break;
            }
            WriteTypeName(param, noSpace: true);
        }

        private void WriteGenericContraints(IEnumerable<IGenericParameter> genericParams)
        {
            if (!genericParams.Any())
                return;

            foreach (IGenericParameter param in genericParams)
            {
                var constraints = GetConstraints(param).ToList();

                if (constraints.Count <= 0)
                    continue;

                WriteSpace();
                WriteKeyword("where");
                WriteTypeName(param);
                WriteSymbol(":", true);

                _writer.WriteList(constraints, c => c());
            }
        }

        private IEnumerable<Action> GetConstraints(IGenericParameter parameter)
        {
            parameter.Attributes.TryGetAttributeOfType("System.Runtime.CompilerServices.NullableAttribute", out ICustomAttribute nullableAttribute);
            if (parameter.MustBeValueType)
                yield return () => WriteKeyword("struct", noSpace: true);
            else
            {
                if (parameter.MustBeReferenceType)
                    yield return () =>
                    {
                        WriteKeyword("class", noSpace: true);

                        if (nullableAttribute != null)
                        {
                            WriteNullableSymbolForReferenceType(GetAttributeArgumentValue<byte>(nullableAttribute), arrayIndex: 0);
                        }
                    };
            }

            var assemblyLocation = parameter.Locations.FirstOrDefault()?.Document?.Location;

            int constraintIndex = 0;
            foreach (var constraint in parameter.Constraints)
            {
                // Skip valuetype because we should get it above.
                if (!TypeHelper.TypesAreEquivalent(constraint, constraint.PlatformType.SystemValueType) && !parameter.MustBeValueType)
                {
                    object nullableAttributeValue = null;
                    if (assemblyLocation != null)
                    {
                        nullableAttributeValue = parameter.GetGenericParameterConstraintConstructorArgument(constraintIndex, "System.Runtime.CompilerServices.NullableAttribute", assemblyLocation, CSharpCciExtensions.NullableConstructorArgumentParser);
                    }

                    constraintIndex++;
                    yield return () => WriteTypeName(constraint, noSpace: true, nullableAttributeArgument: nullableAttributeValue);
                }
            }

            // If it has no other constraint of any type and the parameter contains a NullableAttribute then it might have a notnull constraint
            if (constraintIndex == 0 && !parameter.MustBeValueType && !parameter.MustBeReferenceType && nullableAttribute != null)
            {
                byte value = (byte)GetAttributeArgumentValue<byte>(nullableAttribute);
                if ((value & 1) != 0)
                {
                    yield return () => WriteKeyword("notnull", noSpace: true);
                }
            }

            // new constraint cannot be put on structs and needs to be the last constraint
            if (!parameter.MustBeValueType && parameter.MustHaveDefaultConstructor)
                yield return () => { WriteKeyword("new", noSpace: true); WriteSymbol("("); WriteSymbol(")"); };
        }
    }
}
