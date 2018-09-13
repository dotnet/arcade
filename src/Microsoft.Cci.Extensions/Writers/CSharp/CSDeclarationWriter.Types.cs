// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Extensions;

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter
    {
        public void WriteTypeDeclaration(ITypeDefinition type)
        {
            INamedTypeDefinition namedType = (INamedTypeDefinition)type;

            WriteAttributes(type.Attributes);
            WriteAttributes(type.SecurityAttributes);

            //TODO: We should likely add support for the SerializableAttribute something like:
            // if (type.IsSerializable) WriteFakeAttribute("System.Serializable");
            // But we need also consider if this attribute is filtered out or not but I guess
            // we have the same problem with all the fake attributes at this point.

            if ((type.IsStruct || type.IsClass) && type.Layout != LayoutKind.Auto)
            {
                FakeCustomAttribute structLayout = new FakeCustomAttribute("System.Runtime.InteropServices", "StructLayoutAttribute");
                string layoutKind = string.Format("System.Runtime.InteropServices.LayoutKind.{0}", type.Layout.ToString());

                if (_forCompilationIncludeGlobalprefix)
                    layoutKind = "global::" + layoutKind;

                var args = new List<string>();
                args.Add(layoutKind);

                if (type.SizeOf != 0)
                {
                    string sizeOf = string.Format("Size={0}", type.SizeOf);
                    args.Add(sizeOf);
                }

                if (type.Alignment != 0)
                {
                    string pack = string.Format("Pack={0}", type.Alignment);
                    args.Add(pack);
                }

                if (type.StringFormat != StringFormatKind.Ansi)
                {
                    string charset = string.Format("CharSet={0}System.Runtime.InteropServices.CharSet.{1}", _forCompilationIncludeGlobalprefix ? "global::" : "", type.StringFormat);
                    args.Add(charset);
                }

                if (IncludeAttribute(structLayout))
                    WriteFakeAttribute(structLayout.FullTypeName, args.ToArray());
            }

            WriteVisibility(TypeHelper.TypeVisibilityAsTypeMemberVisibility(type));

            IMethodDefinition invoke = type.GetInvokeMethod();
            if (invoke != null)
            {
                Contract.Assert(type.IsDelegate);

                if (invoke.IsMethodUnsafe()) WriteKeyword("unsafe");
                WriteKeyword("delegate");
                WriteTypeName(invoke.Type);
                WriteIdentifier(namedType.Name);
                if (type.IsGeneric) WriteGenericParameters(type.GenericParameters);
                WriteParameters(invoke.Parameters, invoke.ContainingType);
                if (type.IsGeneric) WriteGenericContraints(type.GenericParameters);
                WriteSymbol(";");
            }
            else
            {
                WriteTypeModifiers(type);
                WriteIdentifier(namedType.Name);
                Contract.Assert(!(type is IGenericTypeInstance), "Currently don't support generic type instances if we hit this then add support");
                if (type.IsGeneric) WriteGenericParameters(type.GenericParameters);
                WriteBaseTypes(type);
                if (type.IsGeneric) WriteGenericContraints(type.GenericParameters);

                if (type.IsEnum)
                    WriteEnumType(type);
            }
        }

        // Note that the metadata order for interfaces may change from one release to another.
        // This isn't an incompatibility in surface area.  So, we must sort our list of base types
        // to reflect this.
        private void WriteBaseTypes(ITypeDefinition type)
        {
            List<ITypeReference> baseTypes = new List<ITypeReference>();

            ITypeReference baseType = GetBaseType(type);

            if (baseType != null)
                baseTypes.Add(baseType);

            baseTypes.AddRange(type.Interfaces.Where(IncludeBaseType).OrderBy((t) => GetTypeName(t), StringComparer.OrdinalIgnoreCase));

            if (baseTypes.Count == 0)
                return;

            WriteSpace();
            WriteSymbol(":", true);

            WriteList(baseTypes, (t) => WriteTypeName(t, noSpace: true));
        }

        private string GetTypeName(ITypeReference type)
        {
            Contract.Requires(type != null);
            NameFormattingOptions namingOptions = NameFormattingOptions.TypeParameters | NameFormattingOptions.UseTypeKeywords;

            if (!_forCompilation)
                namingOptions |= NameFormattingOptions.OmitContainingNamespace;

            string name = TypeHelper.GetTypeName(type, namingOptions);
            return name;
        }

        private ITypeReference GetBaseType(ITypeDefinition type)
        {
            if (type == Dummy.Type)
                return null;

            ITypeReference baseTypeRef = type.BaseClasses.FirstOrDefault();

            if (baseTypeRef == null)
                return null;

            if (baseTypeRef.IsDefaultCSharpBaseType(type))
                return null;

            if (!IncludeBaseType(baseTypeRef))
            {
                return GetBaseType(baseTypeRef.ResolvedType);
            }

            return baseTypeRef;
        }

        private void WriteTypeModifiers(ITypeDefinition type)
        {
            if (type.IsDelegate)
                throw new NotSupportedException("This method doesn't support delegates!");
            else if (type.IsEnum)
                WriteKeyword("enum");
            else if (type.IsValueType)
            {
                if (type.Attributes.HasIsReadOnlyAttribute())
                    WriteKeyword("readonly");

                if (type.Attributes.HasIsByRefLikeAttribute())
                    WriteKeyword("ref");

                WritePartialKeyword();
                WriteKeyword("struct");
            }
            else if (type.IsInterface)
            {
                WritePartialKeyword();
                WriteKeyword("interface");
            }
            else
            {
                if (!type.IsClass)
                    throw new NotSupportedException("Don't understand what kind of type this is!");

                if (type.IsStatic)
                    WriteKeyword("static");
                else if (type.IsSealed)
                    WriteKeyword("sealed");
                else if (type.IsAbstract)
                    WriteKeyword("abstract");

                WritePartialKeyword();
                WriteKeyword("class");
            }
        }

        private void WritePartialKeyword()
        {
            if (_forCompilation)
                WriteKeyword("partial");
        }

        private void WriteEnumType(ITypeDefinition type)
        {
            ITypeReference enumType = type.GetEnumType();

            // Don't write the default type
            if (TypeHelper.TypesAreEquivalent(enumType, type.PlatformType.SystemInt32))
                return;

            WriteSpace();
            WriteSymbol(":", addSpace: true);
            WriteTypeName(enumType, noSpace: true);
        }

        private bool IncludeBaseType(ITypeReference iface)
        {
            ITypeDefinition ifaceType = iface.ResolvedType;

            // We should by default include base types even if we cannot resolve
            // for cases where we are working with standalone assemblies.
            if (ifaceType == Dummy.Type)
                return true;

            return _alwaysIncludeBase || _filter.Include(ifaceType);
        }
    }
}
