// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Cci.Writers.Syntax;

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
                var constraints = GetConstraints(param);

                if (!constraints.Any())
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
            if (parameter.MustBeValueType)
                yield return () => WriteKeyword("struct", noSpace: true);
            else
            {
                if (parameter.MustBeReferenceType)
                    yield return () => WriteKeyword("class", noSpace: true);
            }

            foreach (var constraint in parameter.Constraints)
            {
                // Skip valuetype because we should get it below.
                if (TypeHelper.TypesAreEquivalent(constraint, constraint.PlatformType.SystemValueType) && parameter.MustBeValueType)
                    continue;

                yield return () => WriteTypeName(constraint, noSpace: true);
            }

            // new constraint cannot be put on structs and needs to be the last constraint
            if (!parameter.MustBeValueType && parameter.MustHaveDefaultConstructor)
                yield return () => { WriteKeyword("new", noSpace: true); WriteSymbol("("); WriteSymbol(")"); };
        }
    }
}
