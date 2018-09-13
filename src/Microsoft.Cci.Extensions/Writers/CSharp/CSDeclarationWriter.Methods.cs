// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Writers.Syntax;

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter
    {
        private void WriteMethodDefinition(IMethodDefinition method)
        {
            if (method.IsPropertyOrEventAccessor())
                return;

            WriteMethodPseudoCustomAttributes(method);

            WriteAttributes(method.Attributes);
            WriteAttributes(method.SecurityAttributes);

            if (method.IsDestructor())
            {
                // If platformNotSupportedExceptionMessage is != null we're generating a dummy assembly which means we don't need a destructor at all.
                if(_platformNotSupportedExceptionMessage == null)
                    WriteDestructor(method);

                return;
            }

            string name = method.GetMethodName();

            if (!method.ContainingTypeDefinition.IsInterface)
            {
                if (!method.IsExplicitInterfaceMethod()) WriteVisibility(method.Visibility);
                WriteMethodModifiers(method);
            }
            WriteInterfaceMethodModifiers(method);
            WriteMethodDefinitionSignature(method, name);
            WriteMethodBody(method);
        }

        private void WriteDestructor(IMethodDefinition method)
        {
            WriteSymbol("~");
            WriteIdentifier(((INamedEntity)method.ContainingTypeDefinition).Name);
            WriteSymbol("(");
            WriteSymbol(")", false);
            WriteEmptyBody();
        }

        private void WriteTypeName(ITypeReference type, ITypeReference containingType, bool isDynamic = false)
        {
            var useKeywords = containingType.GetTypeName() != type.GetTypeName();

            WriteTypeName(type, isDynamic: isDynamic, useTypeKeywords: useKeywords);
        }

        private void WriteMethodDefinitionSignature(IMethodDefinition method, string name)
        {
            bool isOperator = method.IsConversionOperator();

            if (!isOperator && !method.IsConstructor)
            {
                WriteAttributes(method.ReturnValueAttributes, true);

                if (method.ReturnValueIsByRef)
                {
                    WriteKeyword("ref");

                    if (method.ReturnValueAttributes.HasIsReadOnlyAttribute())
                        WriteKeyword("readonly");
                }

                // We are ignoring custom modifiers right now, we might need to add them later.
                WriteTypeName(method.Type, method.ContainingType, isDynamic: IsDynamic(method.ReturnValueAttributes));
            }

            if (method.IsExplicitInterfaceMethod() && _forCompilationIncludeGlobalprefix)
                Write("global::");

            WriteIdentifier(name);

            if (isOperator)
            {
                WriteSpace();

                WriteTypeName(method.Type, method.ContainingType);
            }

            Contract.Assert(!(method is IGenericMethodInstance), "Currently don't support generic method instances");
            if (method.IsGeneric)
                WriteGenericParameters(method.GenericParameters);

            WriteParameters(method.Parameters, method.ContainingType, extensionMethod: method.IsExtensionMethod(), acceptsExtraArguments: method.AcceptsExtraArguments);
            if (method.IsGeneric && !method.IsOverride() && !method.IsExplicitInterfaceMethod())
                WriteGenericContraints(method.GenericParameters);
        }

        private void WriteParameters(IEnumerable<IParameterDefinition> parameters, ITypeReference containingType, bool property = false, bool extensionMethod = false, bool acceptsExtraArguments = false)
        {
            string start = property ? "[" : "(";
            string end = property ? "]" : ")";

            WriteSymbol(start);
            _writer.WriteList(parameters, p =>
            {
                WriteParameter(p, containingType, extensionMethod);
                extensionMethod = false;
            });

            if (acceptsExtraArguments)
            {
                if (parameters.Any())
                    _writer.WriteSymbol(",");
                _writer.WriteSpace();
                _writer.Write("__arglist");
            }

            WriteSymbol(end);
        }

        private void WriteParameter(IParameterDefinition parameter, ITypeReference containingType, bool extensionMethod)
        {
            WriteAttributes(parameter.Attributes, true);

            if (extensionMethod)
                WriteKeyword("this");

            if (parameter.IsParameterArray)
                WriteKeyword("params");

            if (parameter.IsOut && !parameter.IsIn && parameter.IsByReference)
            {
                WriteKeyword("out");
            }
            else
            {
                // For In/Out we should not emit them until we find a scenario that is needs thems.
                //if (parameter.IsIn)
                //   WriteFakeAttribute("System.Runtime.InteropServices.In", writeInline: true);
                //if (parameter.IsOut)
                //    WriteFakeAttribute("System.Runtime.InteropServices.Out", writeInline: true);
                if (parameter.IsByReference)
                {
                    if (parameter.Attributes.HasIsReadOnlyAttribute())
                    {
                        WriteKeyword("in");
                    }
                    else
                    {
                        WriteKeyword("ref");
                    }
                }
            }

            WriteTypeName(parameter.Type, containingType, isDynamic: IsDynamic(parameter.Attributes));
            WriteIdentifier(parameter.Name);
            if (parameter.IsOptional && parameter.HasDefaultValue)
            {
                WriteSymbol(" = ");
                WriteMetadataConstant(parameter.DefaultValue, parameter.Type);
            }
        }

        private void WriteInterfaceMethodModifiers(IMethodDefinition method)
        {
            if (method.GetHiddenBaseMethod(_filter) != Dummy.Method)
                WriteKeyword("new");
        }

        private void WriteMethodModifiers(IMethodDefinition method)
        {
            if (method.IsMethodUnsafe())
                WriteKeyword("unsafe");

            if (method.IsStatic)
                WriteKeyword("static");

            if (method.IsPlatformInvoke)
                WriteKeyword("extern");

            if (method.IsVirtual)
            {
                if (method.IsNewSlot)
                {
                    if (method.IsAbstract)
                        WriteKeyword("abstract");
                    else if (!method.IsSealed) // non-virtual interfaces implementations are sealed virtual newslots
                        WriteKeyword("virtual");
                }
                else
                {
                    if (method.IsAbstract)
                        WriteKeyword("abstract");
                    else if (method.IsSealed)
                        WriteKeyword("sealed");
                    WriteKeyword("override");
                }
            }
        }

        private void WriteMethodBody(IMethodDefinition method)
        {
            if (method.IsAbstract || !_forCompilation || method.IsPlatformInvoke)
            {
                WriteSymbol(";");
                return;
            }

            if (method.IsConstructor)
                WriteBaseConstructorCall(method.ContainingTypeDefinition);

            // Write Dummy Body
            WriteSpace();
            WriteSymbol("{", true);

            if (_platformNotSupportedExceptionMessage != null && !method.IsDispose())
            {
                Write("throw new ");
                if (_forCompilationIncludeGlobalprefix)
                    Write("global::");

                Write("System.PlatformNotSupportedException(");

                if (_platformNotSupportedExceptionMessage.StartsWith("SR."))
                {
                    if (_forCompilationIncludeGlobalprefix)
                        Write("global::");
                    Write($"System.{ _platformNotSupportedExceptionMessage}");
                }
                else if (_platformNotSupportedExceptionMessage.Length > 0)
                    Write($"\"{_platformNotSupportedExceptionMessage}\"");

                 Write(");");
            }
            else if (NeedsMethodBodyForCompilation(method))
            {
                Write("throw null; ");
            }

            WriteSymbol("}");
        }

        private bool NeedsMethodBodyForCompilation(IMethodDefinition method)
        {
            // Structs cannot have empty constructors so we need a body
            if (method.ContainingTypeDefinition.IsValueType && method.IsConstructor)
                return true;

            // Compiler requires out parameters to be initialized 
            if (method.Parameters.Any(p => p.IsOut))
                return true;

            // For non-void returning methods we need a body. 
            if (!TypeHelper.TypesAreEquivalent(method.Type, method.ContainingTypeDefinition.PlatformType.SystemVoid))
                return true;

            return false;
        }

        private void WritePrivateConstructor(ITypeDefinition type)
        {
            if (!_forCompilation ||
                type.IsInterface ||
                type.IsEnum ||
                type.IsDelegate ||
                type.IsValueType ||
                type.IsStatic)
                return;

            WriteVisibility(TypeMemberVisibility.Assembly);
            WriteIdentifier(((INamedEntity)type).Name);
            WriteSymbol("(");
            WriteSymbol(")");
            WriteBaseConstructorCall(type);
            WriteEmptyBody();
        }

        private void WriteBaseConstructorCall(ITypeDefinition type)
        {
            if (!_forCompilation)
                return;

            ITypeDefinition baseType = type.BaseClasses.FirstOrDefault().GetDefinitionOrNull();

            if (baseType == null)
                return;

            var ctors = baseType.Methods.Where(m => m.IsConstructor && _filter.Include(m) && !m.Attributes.Any(a => a.IsObsoleteWithUsageTreatedAsCompilationError()));

            var defaultCtor = ctors.Where(c => c.ParameterCount == 0);

            // Don't need a base call if we have a default constructor
            if (defaultCtor.Any())
                return;

            var ctor = ctors.FirstOrDefault();

            if (ctor == null)
                return;

            WriteSpace();
            WriteSymbol(":", true);
            WriteKeyword("base");
            WriteSymbol("(");
            _writer.WriteList(ctor.Parameters, p => WriteDefaultOf(p.Type));
            WriteSymbol(")");
        }

        private void WriteEmptyBody()
        {
            if (!_forCompilation)
            {
                WriteSymbol(";");
            }
            else
            {
                WriteSpace();
                WriteSymbol("{", true);
                WriteSymbol("}");
            }
        }

        private void WriteDefaultOf(ITypeReference type)
        {
            WriteKeyword("default", true);
            WriteSymbol("(");
            WriteTypeName(type, true);
            WriteSymbol(")");
        }

        public static IDefinition GetDummyConstructor(ITypeDefinition type)
        {
            return new DummyInternalConstructor() { ContainingType = type };
        }

        private class DummyInternalConstructor : IDefinition
        {
            public ITypeDefinition ContainingType { get; set; }

            public IEnumerable<ICustomAttribute> Attributes
            {
                get { throw new System.NotImplementedException(); }
            }

            public void Dispatch(IMetadataVisitor visitor)
            {
                throw new System.NotImplementedException();
            }

            public IEnumerable<ILocation> Locations
            {
                get { throw new System.NotImplementedException(); }
            }

            public void DispatchAsReference(IMetadataVisitor visitor)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
