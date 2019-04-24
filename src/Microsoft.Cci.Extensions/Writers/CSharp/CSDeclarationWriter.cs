// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Writers.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter : ICciDeclarationWriter
    {
        public static readonly Version LangVersion7_0 = new Version(7, 0);
        public static readonly Version LangVersion7_3 = new Version(7, 3);
        public static readonly Version LangVersion8_0 = new Version(8, 0);

        public static readonly Version LangVersionDefault = LangVersion7_0;
        public static readonly Version LangVersionLatest = LangVersion7_3;
        public static readonly Version LangVersionPreview = LangVersion8_0;

        private readonly ISyntaxWriter _writer;
        private readonly ICciFilter _filter;
        private bool _forCompilation;
        private bool _forCompilationIncludeGlobalprefix;
        private string _platformNotSupportedExceptionMessage;
        private bool _includeFakeAttributes;
        private bool _alwaysIncludeBase;

        public CSDeclarationWriter(ISyntaxWriter writer)
            : this(writer, new PublicOnlyCciFilter())
        {
        }

        public CSDeclarationWriter(ISyntaxWriter writer, ICciFilter filter)
            : this(writer, filter, true)
        {
        }

        public CSDeclarationWriter(ISyntaxWriter writer, ICciFilter filter, bool forCompilation)
        {
            Contract.Requires(writer != null);
            _writer = writer;
            _filter = filter;
            _forCompilation = forCompilation;
            _forCompilationIncludeGlobalprefix = false;
            _platformNotSupportedExceptionMessage = null;
            _includeFakeAttributes = false;
            _alwaysIncludeBase = false;
        }

        public CSDeclarationWriter(ISyntaxWriter writer, ICciFilter filter, bool forCompilation, bool includePseudoCustomAttributes = false)
            : this(writer, filter, forCompilation)
        {
            _includeFakeAttributes = includePseudoCustomAttributes;
        }

        public bool ForCompilation
        {
            get { return _forCompilation; }
            set { _forCompilation = value; }
        }

        public bool ForCompilationIncludeGlobalPrefix
        {
            get { return _forCompilationIncludeGlobalprefix; }
            set { _forCompilationIncludeGlobalprefix = value; }
        }

        public string PlatformNotSupportedExceptionMessage
        {
            get { return _platformNotSupportedExceptionMessage; }
            set { _platformNotSupportedExceptionMessage = value; }
        }

        public bool AlwaysIncludeBase
        {
            get { return _alwaysIncludeBase; }
            set { _alwaysIncludeBase = value; }
        }

        public ISyntaxWriter SyntaxtWriter { get { return _writer; } }

        public ICciFilter Filter { get { return _filter; } }

        public Version LangVersion { get; set; }

        public void WriteDeclaration(IDefinition definition)
        {
            if (definition == null)
                return;

            IAssembly assembly = definition as IAssembly;
            if (assembly != null)
            {
                WriteAssemblyDeclaration(assembly);
                return;
            }

            INamespaceDefinition ns = definition as INamespaceDefinition;
            if (ns != null)
            {
                WriteNamespaceDeclaration(ns);
                return;
            }

            ITypeDefinition type = definition as ITypeDefinition;
            if (type != null)
            {
                WriteTypeDeclaration(type);
                return;
            }

            ITypeDefinitionMember member = definition as ITypeDefinitionMember;
            if (member != null)
            {
                WriteMemberDeclaration(member);
                return;
            }

            DummyInternalConstructor ctor = definition as DummyInternalConstructor;
            if (ctor != null)
            {
                WritePrivateConstructor(ctor.ContainingType);
                return;
            }

            INamedEntity named = definition as INamedEntity;
            if (named != null)
            {
                WriteIdentifier(named.Name);
                return;
            }

            _writer.Write("Unknown definition type {0}", definition.ToString());
        }

        public void WriteAttribute(ICustomAttribute attribute)
        {
            WriteSymbol("[");
            WriteAttribute(attribute, null);
            WriteSymbol("]");
        }

        public void WriteAssemblyDeclaration(IAssembly assembly)
        {
            WriteAttributes(assembly.Attributes, prefix: "assembly");
            WriteAttributes(assembly.SecurityAttributes, prefix: "assembly");
        }

        public void WriteMemberDeclaration(ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
            {
                WriteMethodDefinition(method);
                return;
            }

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
            {
                WritePropertyDefinition(property);
                return;
            }

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
            {
                WriteEventDefinition(evnt);
                return;
            }

            IFieldDefinition field = member as IFieldDefinition;
            if (field != null)
            {
                WriteFieldDefinition(field);
                return;
            }

            _writer.Write("Unknown member definitions type {0}", member.ToString());
        }

        private void WriteVisibility(TypeMemberVisibility visibility)
        {
            switch (visibility)
            {
                case TypeMemberVisibility.Public:
                    WriteKeyword("public"); break;
                case TypeMemberVisibility.Private:
                    WriteKeyword("private"); break;
                case TypeMemberVisibility.Assembly:
                    WriteKeyword("internal"); break;
                case TypeMemberVisibility.Family:
                    WriteKeyword("protected"); break;
                case TypeMemberVisibility.FamilyOrAssembly:
                    WriteKeyword("protected"); WriteKeyword("internal"); break;
                case TypeMemberVisibility.FamilyAndAssembly:
                    WriteKeyword("internal"); WriteKeyword("protected"); break; // Is this right?
                default:
                    WriteKeyword("<Unknown-Visibility>"); break;
            }
        }

        private void WriteCustomModifiers(IEnumerable<ICustomModifier> modifiers)
        {
            foreach (ICustomModifier modifier in modifiers)
            {
                if (modifier.Modifier.FullName() == "System.Runtime.CompilerServices.IsVolatile")
                    WriteKeyword("volatile");
            }
        }

        // Writer Helpers these are the only methods that should directly access _writer
        private void WriteKeyword(string keyword, bool noSpace = false)
        {
            _writer.WriteKeyword(keyword);
            if (!noSpace) WriteSpace();
        }

        private void WriteSymbol(string symbol, bool addSpace = false)
        {
            _writer.WriteSymbol(symbol);
            if (addSpace)
                WriteSpace();
        }

        private void Write(string literal)
        {
            _writer.Write(literal);
        }

        private object GetAttributeArgumentValue<TType>(ICustomAttribute attribute, object defaultValue = null)
        {
            object result = defaultValue;
            if (attribute != null)
            {
                object argument = attribute.Arguments.FirstOrDefault();
                if (argument is IMetadataCreateArray argumentArray)
                {
                    TType[] array = new TType[argumentArray.Sizes.Single()];
                    int i = 0;
                    foreach (IMetadataExpression value in argumentArray.Initializers)
                    {
                        array[i++] = (TType)(value as IMetadataConstant).Value;
                    }
                    result = array;
                }
                else if (argument is IMetadataConstant value)
                {
                    result = (TType)value.Value;
                }
            }

            return result;
        }

        private void WriteNullableSymbol(object nullableAttributeArgument, bool isNullableValueType, int arrayIndex)
        {
            if (isNullableValueType)
            {
                _writer.WriteSymbol("?");
                return;
            }

            if (nullableAttributeArgument is null)
            {
                return;
            }

            byte attributeByteValue;
            if (nullableAttributeArgument is byte[] attributeArray)
            {
                attributeByteValue = attributeArray[arrayIndex];
            }
            else
            {
                attributeByteValue = (byte)nullableAttributeArgument;
            }

            if ((attributeByteValue & 2) != 0)
            {
                _writer.WriteSymbol("?");
            }
        }

        private bool IsDynamicType(object dynamicAttributeArgument, int arrayIndex)
        {
            if (dynamicAttributeArgument == null)
            {
                return false;
            }

            if (dynamicAttributeArgument is bool[] attributeArray)
            {
                return attributeArray[arrayIndex];
            }

            return (bool)dynamicAttributeArgument;

        }

        private int WriteTypeNameRecursive(ITypeReference type, NameFormattingOptions namingOptions,
            string[] valueTupleNames, ref int valueTupleNameIndex, object nullableAttributeArgument, object dynamicAttributeArgument,
            int typeDepth = 0)
        {
            void WriteTypeNameInner(ITypeReference typeReference)
            {
                if (IsDynamicType(dynamicAttributeArgument, typeDepth))
                {
                    _writer.WriteKeyword("dynamic");
                }
                else
                {
                    string name = TypeHelper.GetTypeName(typeReference, namingOptions);

                    if (CSharpCciExtensions.IsKeyword(name))
                        _writer.WriteKeyword(name);
                    else
                        _writer.WriteTypeName(name);
                }
            }

            int genericArguments = 0;
            bool hasNullableAttribute = nullableAttributeArgument != null;
            bool hasDynamicAttribute = dynamicAttributeArgument is bool ? (bool)dynamicAttributeArgument : dynamicAttributeArgument != null;
            bool isNullableValueType = false;
            bool isValueTuple;
            if (type is IGenericTypeInstanceReference genericType && 
                ((isValueTuple = genericType.IsValueTuple()) || hasNullableAttribute || hasDynamicAttribute))
            {
                int genericArgumentsInChildTypes = 0;
                int valueTupleLocalIndex = valueTupleNameIndex;
                isNullableValueType = genericType.IsNullableValueType();

                if (isNullableValueType)
                {
                    namingOptions &= ~NameFormattingOptions.ContractNullable;
                }
                else
                {
                    if (isValueTuple)
                    {
                        _writer.WriteSymbol("(");
                        valueTupleNameIndex += genericType.GenericArguments.Count();
                    }
                    else
                    {
                        WriteTypeNameInner(type);
                        _writer.WriteSymbol("<");
                    }
                }

                int i = 0;
                foreach (var parameter in genericType.GenericArguments)
                {
                    if (i != 0)
                    {
                        _writer.WriteSymbol(",");
                        _writer.WriteSpace();
                    }

                    string valueTupleName =  isValueTuple ? valueTupleNames?[valueTupleLocalIndex + i] : null;
                    int destinationTypeDepth = typeDepth + i + genericArgumentsInChildTypes + 1;
                    genericArgumentsInChildTypes += WriteTypeNameRecursive(parameter, namingOptions, valueTupleNames, ref valueTupleNameIndex, nullableAttributeArgument, dynamicAttributeArgument, destinationTypeDepth);

                    if (valueTupleName != null)
                    {
                        _writer.WriteSpace();
                        _writer.WriteIdentifier(valueTupleName);
                    }

                    i++;
                }

                genericArguments = i;

                if (!isNullableValueType)
                {
                    _writer.WriteSymbol(isValueTuple ? ")" : ">");
                }
            }
            else if (type is IArrayType arrayType && (hasNullableAttribute || hasDynamicAttribute))
            {
                WriteTypeNameRecursive(arrayType.ElementType, namingOptions, valueTupleNames, ref valueTupleNameIndex,
                    nullableAttributeArgument, dynamicAttributeArgument, typeDepth + 1);
                WriteSymbol("[");
                WriteSymbol("]");
            }
            else
            {
                WriteTypeNameInner(type);
            }

            WriteNullableSymbol(nullableAttributeArgument, isNullableValueType, typeDepth);

            return genericArguments;
        }

        private void WriteTypeName(ITypeReference type, IEnumerable<ICustomAttribute> attributes, bool noSpace = false, bool useTypeKeywords = true,
            bool omitGenericTypeList = false)
        {
            attributes.TryGetAttributeOfType("System.Runtime.CompilerServices.NullableAttribute", out ICustomAttribute nullableAttribute);
            bool hasDynamicAttribute = attributes.TryGetAttributeOfType("System.Runtime.CompilerServices.DynamicAttribute", out ICustomAttribute dynamicAttribute);

            object nullableAttributeArgument = GetAttributeArgumentValue<byte>(nullableAttribute);
            object dynamicAttributeArgument = GetAttributeArgumentValue<bool>(dynamicAttribute, defaultValue: hasDynamicAttribute);

            WriteTypeName(type, noSpace, useTypeKeywords, omitGenericTypeList, nullableAttributeArgument, dynamicAttributeArgument, attributes?.GetValueTupleNames());
        }

        private void WriteTypeName(ITypeReference type, bool noSpace = false, bool useTypeKeywords = true,
            bool omitGenericTypeList = false, object nullableAttributeArgument = null, object dynamicAttributeArgument = null, string[] valueTupleNames = null)
        {
            NameFormattingOptions namingOptions = NameFormattingOptions.TypeParameters | NameFormattingOptions.ContractNullable;

            if (useTypeKeywords)
                namingOptions |= NameFormattingOptions.UseTypeKeywords;

            if (_forCompilationIncludeGlobalprefix)
                namingOptions |= NameFormattingOptions.UseGlobalPrefix;

            if (!_forCompilation)
                namingOptions |= NameFormattingOptions.OmitContainingNamespace;

            if (omitGenericTypeList)
                namingOptions |= NameFormattingOptions.EmptyTypeParameterList;

            if (nullableAttributeArgument != null ||
                (dynamicAttributeArgument is bool ? (bool)dynamicAttributeArgument : dynamicAttributeArgument != null))
                namingOptions |= NameFormattingOptions.OmitTypeArguments;

            int valueTupleNameIndex = 0;
            WriteTypeNameRecursive(type, namingOptions, valueTupleNames, ref valueTupleNameIndex, nullableAttributeArgument, dynamicAttributeArgument);

            if (!noSpace) WriteSpace();
        }

        public void WriteIdentifier(string id)
        {
            WriteIdentifier(id, true);
        }

        public void WriteIdentifier(string id, bool escape)
        {
            // Escape keywords
            if (escape && CSharpCciExtensions.IsKeyword(id))
                id = "@" + id;
            _writer.WriteIdentifier(id);
        }

        private void WriteIdentifier(IName name)
        {
            WriteIdentifier(name.Value);
        }

        private void WriteSpace()
        {
            _writer.Write(" ");
        }

        private void WriteList<T>(IEnumerable<T> list, Action<T> writeItem)
        {
            _writer.WriteList(list, writeItem);
        }
    }
}
