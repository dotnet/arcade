// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.ApiVersioning.Analyzers
{
    internal class TypeVersionChecker
    {
        private static readonly string[] IgnorableVersionedTypeNames =
        {
            "System.DateTimeOffset",
            "System.Guid",
            "System.Nullable`1",
            "System.TimeSpan",
            "System.Threading.CancellationToken",
            "System.Threading.Tasks.Task",
            "System.Threading.Tasks.Task`1",
            "System.Collections.Generic.List`1",
            "Microsoft.AspNetCore.Mvc.IActionResult",
            "Microsoft.AspNetCore.Mvc.ActionResult"
        };

        public TypeVersionChecker(Compilation compilation)
        {
            Compilation = compilation;
            IgnorableVersionedTypes = IgnorableVersionedTypeNames.Select(n => Compilation.GetTypeByMetadataName(n))
                .Where(t => t != null)
                .ToList();
        }

        private Compilation Compilation { get; }

        public IReadOnlyList<INamedTypeSymbol> IgnorableVersionedTypes { get; }

        public bool IsVersionable(ITypeSymbol symbol)
        {
            if (IsVersionableSpecialType(symbol))
            {
                return true;
            }

            if (IgnorableVersionedTypes.Any(symbol.Equals))
            {
                return true;
            }

            if (symbol.OriginalDefinition != null && IgnorableVersionedTypes.Any(symbol.OriginalDefinition.Equals))
            {
                return true;
            }

            return IsVersioned(symbol);
        }

        private bool IsVersionableSpecialType(ITypeSymbol symbol)
        {
            switch (symbol.SpecialType)
            {
                case SpecialType.System_Object:
                case SpecialType.System_Void:
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Decimal:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_String:
                case SpecialType.System_IntPtr:
                case SpecialType.System_UIntPtr:
                case SpecialType.System_Array:
                case SpecialType.System_Collections_IEnumerable:
                case SpecialType.System_Collections_Generic_IEnumerable_T:
                case SpecialType.System_Collections_Generic_IList_T:
                case SpecialType.System_Collections_Generic_ICollection_T:
                case SpecialType.System_Collections_IEnumerator:
                case SpecialType.System_Collections_Generic_IEnumerator_T:
                case SpecialType.System_Collections_Generic_IReadOnlyList_T:
                case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
                case SpecialType.System_Nullable_T:
                case SpecialType.System_DateTime:
                    // allowable Special Types
                    return true;


                case SpecialType.None:
                    // Not Special Type, fall through to checking for known types.
                    return false;
                case SpecialType.System_ArgIterator:
                case SpecialType.System_AsyncCallback:
                case SpecialType.System_Delegate:
                case SpecialType.System_Enum:
                case SpecialType.System_IAsyncResult:
                case SpecialType.System_IDisposable:
                case SpecialType.System_MulticastDelegate:
                case SpecialType.System_RuntimeArgumentHandle:
                case SpecialType.System_RuntimeFieldHandle:
                case SpecialType.System_RuntimeMethodHandle:
                case SpecialType.System_RuntimeTypeHandle:
                case SpecialType.System_Runtime_CompilerServices_IsVolatile:
                case SpecialType.System_TypedReference:
                case SpecialType.System_ValueType:
                    // Bad special types
                    return false;
                default:
                    throw new InvalidOperationException($"Unknown SpecialType Value: {symbol.SpecialType}");
            }
        }

        private bool IsVersioned(ITypeSymbol type)
        {
            return type.ToDisplayString().Contains("Api.v");
        }
    }
}
