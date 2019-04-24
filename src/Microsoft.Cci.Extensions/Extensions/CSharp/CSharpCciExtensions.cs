// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Filters;
using Microsoft.Cci.Writers.CSharp;
using Microsoft.Cci.Writers.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using SRMetadataReader = System.Reflection.Metadata.MetadataReader;

namespace Microsoft.Cci.Extensions.CSharp
{
    public static class CSharpCciExtensions
    {
        public static string GetCSharpDeclaration(this IDefinition definition, bool includeAttributes = false)
        {
            using (var stringWriter = new StringWriter())
            {
                using (var syntaxWriter = new TextSyntaxWriter(stringWriter))
                {
                    var writer = new CSDeclarationWriter(syntaxWriter, new AttributesFilter(includeAttributes), false, true);

                    var nsp = definition as INamespaceDefinition;
                    var typeDefinition = definition as ITypeDefinition;
                    var member = definition as ITypeDefinitionMember;

                    if (nsp != null)
                        writer.WriteNamespaceDeclaration(nsp);
                    else if (typeDefinition != null)
                        writer.WriteTypeDeclaration(typeDefinition);
                    else if (member != null)
                    {
                        var method = member as IMethodDefinition;
                        if (method != null && method.IsPropertyOrEventAccessor())
                            WriteAccessor(syntaxWriter, method);
                        else
                            writer.WriteMemberDeclaration(member);
                    }
                }

                return stringWriter.ToString();
            }
        }

        private static void WriteAccessor(ISyntaxWriter syntaxWriter, IMethodDefinition method)
        {
            var accessorKeyword = GetAccessorKeyword(method);
            syntaxWriter.WriteKeyword(accessorKeyword);
            syntaxWriter.WriteSymbol(";");
        }

        private static string GetAccessorKeyword(IMethodDefinition method)
        {
            switch (method.GetAccessorType())
            {
                case AccessorType.EventAdder:
                    return "add";
                case AccessorType.EventRemover:
                    return "remove";
                case AccessorType.PropertySetter:
                    return "set";
                case AccessorType.PropertyGetter:
                    return "get";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool IsDefaultCSharpBaseType(this ITypeReference baseType, ITypeDefinition type)
        {
            Contract.Requires(baseType != null);
            Contract.Requires(type != null);

            if (baseType.AreEquivalent("System.Object"))
                return true;

            if (type.IsValueType && baseType.AreEquivalent("System.ValueType"))
                return true;

            if (type.IsEnum && baseType.AreEquivalent("System.Enum"))
                return true;

            return false;
        }

        public static IMethodDefinition GetInvokeMethod(this ITypeDefinition type)
        {
            if (!type.IsDelegate)
                return null;

            foreach (var method in type.Methods)
                if (method.Name.Value == "Invoke")
                    return method;

            throw new InvalidOperationException(String.Format("All delegates should have an Invoke method, but {0} doesn't have one.", type.FullName()));
        }

        public static ITypeReference GetEnumType(this ITypeDefinition type)
        {
            if (!type.IsEnum)
                return null;

            foreach (var field in type.Fields)
                if (field.Name.Value == "value__")
                    return field.Type;

            throw new InvalidOperationException("All enums should have a value__ field!");
        }

        public static bool IsOrContainsReferenceType(this ITypeReference type)
        {
            Queue<ITypeReference> typesToCheck = new Queue<ITypeReference>();
            HashSet<ITypeReference> visited = new HashSet<ITypeReference>();

            typesToCheck.Enqueue(type);

            while (typesToCheck.Count != 0)
            {
                var typeToCheck = typesToCheck.Dequeue();
                visited.Add(typeToCheck);

                var resolvedType = typeToCheck.ResolvedType;

                // If it is dummy we cannot really check so assume it does because that is will be the most conservative
                if (resolvedType is Dummy)
                    return true;

                if (resolvedType.IsReferenceType)
                    return true;

                // ByReference<T> is a special type understood by runtime to hold a ref T.
                if (resolvedType.AreEquivalent("System.ByReference<T>"))
                    return true;

                foreach (var field in resolvedType.Fields.Where(f => !f.IsStatic))
                {
                    if (!visited.Contains(field.Type))
                    {
                        typesToCheck.Enqueue(field.Type);
                    }
                }
            }

            return false;
        }

        public static bool IsConversionOperator(this IMethodDefinition method)
        {
            return (method.IsSpecialName &&
                (method.Name.Value == "op_Explicit" || method.Name.Value == "op_Implicit"));
        }

        public static bool IsExplicitInterfaceMember(this ITypeDefinitionMember member)
        {
            var method = member as IMethodDefinition;
            if (method != null)
            {
                return method.IsExplicitInterfaceMethod();
            }

            var property = member as IPropertyDefinition;
            if (property != null)
            {
                return property.IsExplicitInterfaceProperty();
            }

            return false;
        }

        public static bool IsExplicitInterfaceMethod(this IMethodDefinition method)
        {
            return MemberHelper.GetExplicitlyOverriddenMethods(method).Any();
        }

        public static bool IsExplicitInterfaceProperty(this IPropertyDefinition property)
        {
            if (property.Getter != null && property.Getter.ResolvedMethod != null)
            {
                return property.Getter.ResolvedMethod.IsExplicitInterfaceMethod();
            }

            if (property.Setter != null && property.Setter.ResolvedMethod != null)
            {
                return property.Setter.ResolvedMethod.IsExplicitInterfaceMethod();
            }

            return false;
        }

        public static bool IsInterfaceImplementation(this ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
                return method.IsInterfaceImplementation();

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
                return property.Accessors.Any(m => m.ResolvedMethod.IsInterfaceImplementation());

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
                return evnt.Accessors.Any(m => m.ResolvedMethod.IsInterfaceImplementation());

            return false;
        }

        public static bool IsInterfaceImplementation(this IMethodDefinition method)
        {
            return MemberHelper.GetImplicitlyImplementedInterfaceMethods(method).Any()
                || MemberHelper.GetExplicitlyOverriddenMethods(method).Any();
        }

        public static bool IsAbstract(this ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
                return method.IsAbstract;

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
                return property.Accessors.Any(m => m.ResolvedMethod.IsAbstract);

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
                return evnt.Accessors.Any(m => m.ResolvedMethod.IsAbstract);

            return false;
        }

        public static bool IsVirtual(this ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
                return method.IsVirtual;

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
                return property.Accessors.Any(m => m.ResolvedMethod.IsVirtual);

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
                return evnt.Accessors.Any(m => m.ResolvedMethod.IsVirtual);

            return false;
        }

        public static bool IsNewSlot(this ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
                return method.IsNewSlot;

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
                return property.Accessors.Any(m => m.ResolvedMethod.IsNewSlot);

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
                return evnt.Accessors.Any(m => m.ResolvedMethod.IsNewSlot);

            return false;
        }

        public static bool IsSealed(this ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
                return method.IsSealed;

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
                return property.Accessors.Any(m => m.ResolvedMethod.IsSealed);

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
                return evnt.Accessors.Any(m => m.ResolvedMethod.IsSealed);

            return false;
        }

        public static bool IsOverride(this ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
                return method.IsOverride();

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
                return property.Accessors.Any(m => m.ResolvedMethod.IsOverride());

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
                return evnt.Accessors.Any(m => m.ResolvedMethod.IsOverride());

            return false;
        }

        public static bool IsOverride(this IMethodDefinition method)
        {
            return method.IsVirtual && !method.IsNewSlot;
        }

        public static bool IsUnsafeType(this ITypeReference type)
        {
            return type.TypeCode == PrimitiveTypeCode.Pointer;
        }

        public static bool IsMethodUnsafe(this IMethodDefinition method)
        {
            foreach (var p in method.Parameters)
            {
                if (p.Type.IsUnsafeType())
                    return true;
            }
            if (method.Type.IsUnsafeType())
                return true;
            return false;
        }

        public static bool IsDestructor(this IMethodDefinition methodDefinition)
        {
            if (methodDefinition.ContainingTypeDefinition.IsValueType) return false; //only classes can have destructors
            if (methodDefinition.ParameterCount == 0 && methodDefinition.IsVirtual &&
              methodDefinition.Visibility == TypeMemberVisibility.Family && methodDefinition.Name.Value == "Finalize")
            {
                // Should we make sure that this Finalize method overrides the protected System.Object.Finalize?
                return true;
            }
            return false;
        }

        public static bool IsDispose(this IMethodDefinition methodDefinition)
        {
            if ((methodDefinition.Name.Value != "Dispose" && methodDefinition.Name.Value != "System.IDisposable.Dispose") || methodDefinition.ParameterCount > 1 ||
                !TypeHelper.TypesAreEquivalent(methodDefinition.Type, methodDefinition.ContainingTypeDefinition.PlatformType.SystemVoid))
            {
                return false;
            }

            if (methodDefinition.ParameterCount == 1 && !TypeHelper.TypesAreEquivalent(methodDefinition.Parameters.First().Type, methodDefinition.ContainingTypeDefinition.PlatformType.SystemBoolean))
            {
                // Dispose(Boolean) its only parameter should be bool
                return false;
            }

            return true;
        }

        public static bool IsAssembly(this ITypeDefinition type)
        {
            return type.GetVisibility() == TypeMemberVisibility.FamilyAndAssembly ||
                   type.GetVisibility() == TypeMemberVisibility.Assembly;
        }

        public static bool IsAssembly(this ITypeDefinitionMember member)
        {
            return member.Visibility == TypeMemberVisibility.FamilyAndAssembly ||
                   member.Visibility == TypeMemberVisibility.Assembly;
        }

        public static bool InSameUnit(ITypeDefinition type1, ITypeDefinition type2)
        {
            IUnit unit1 = TypeHelper.GetDefiningUnit(type1);
            IUnit unit2 = TypeHelper.GetDefiningUnit(type2);

            return UnitHelper.UnitsAreEquivalent(unit1, unit2);
        }

        public static bool InSameUnit(ITypeDefinitionMember member1, ITypeDefinitionMember member2)
        {
            return InSameUnit(member1.ContainingTypeDefinition, member2.ContainingTypeDefinition);
        }

        public static ITypeReference GetReturnType(this ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
                return method.Type;

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
                return property.Type;

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
                return evnt.Type;

            IFieldDefinition field = member as IFieldDefinition;
            if (field != null)
                return field.Type;

            return null;
        }

        public static IFieldDefinition GetHiddenBaseField(this IFieldDefinition field, ICciFilter filter = null)
        {
            foreach (ITypeReference baseClassRef in field.ContainingTypeDefinition.GetAllBaseTypes())
            {
                ITypeDefinition baseClass = baseClassRef.ResolvedType;
                foreach (IFieldDefinition baseField in baseClass.GetMembersNamed(field.Name, false).OfType<IFieldDefinition>())
                {
                    if (baseField.Visibility == TypeMemberVisibility.Private) continue;

                    if (IsAssembly(baseField) && !InSameUnit(baseField, field))
                        continue;

                    if (filter != null && !filter.Include(baseField))
                        continue;

                    return baseField;
                }
            }
            return Dummy.Field;
        }

        public static IEventDefinition GetHiddenBaseEvent(this IEventDefinition evnt, ICciFilter filter = null)
        {
            IMethodDefinition eventRep = evnt.Adder.ResolvedMethod;
            if (eventRep.IsVirtual && !eventRep.IsNewSlot) return Dummy.Event;   // an override

            foreach (ITypeReference baseClassRef in evnt.ContainingTypeDefinition.GetAllBaseTypes())
            {
                ITypeDefinition baseClass = baseClassRef.ResolvedType;
                foreach (IEventDefinition baseEvent in baseClass.GetMembersNamed(evnt.Name, false).OfType<IEventDefinition>())
                {
                    if (baseEvent.Visibility == TypeMemberVisibility.Private) continue;

                    if (IsAssembly(baseEvent) && !InSameUnit(baseEvent, evnt))
                        continue;

                    if (filter != null && !filter.Include(baseEvent))
                        continue;

                    return baseEvent;
                }
            }
            return Dummy.Event;
        }

        public static IPropertyDefinition GetHiddenBaseProperty(this IPropertyDefinition property, ICciFilter filter = null)
        {
            IMethodDefinition propertyRep = property.Accessors.First().ResolvedMethod;
            if (propertyRep.IsVirtual && !propertyRep.IsNewSlot) return Dummy.Property;   // an override

            ITypeDefinition type = property.ContainingTypeDefinition;

            foreach (ITypeReference baseClassRef in type.GetAllBaseTypes())
            {
                ITypeDefinition baseClass = baseClassRef.ResolvedType;
                foreach (IPropertyDefinition baseProperty in baseClass.GetMembersNamed(property.Name, false).OfType<IPropertyDefinition>())
                {
                    if (baseProperty.Visibility == TypeMemberVisibility.Private) continue;

                    if (IsAssembly(baseProperty) && !InSameUnit(baseProperty, property))
                        continue;

                    if (filter != null && !filter.Include(baseProperty))
                        continue;

                    if (SignaturesParametersAreEqual(property, baseProperty))
                        return baseProperty;
                }
            }
            return Dummy.Property;
        }

        public static IMethodDefinition GetHiddenBaseMethod(this IMethodDefinition method, ICciFilter filter = null)
        {
            if (method.IsConstructor) return Dummy.Method;
            if (method.IsVirtual && !method.IsNewSlot) return Dummy.Method;   // an override

            ITypeDefinition type = method.ContainingTypeDefinition;

            foreach (ITypeReference baseClassRef in type.GetAllBaseTypes())
            {
                ITypeDefinition baseClass = baseClassRef.ResolvedType;
                foreach (IMethodDefinition baseMethod in baseClass.GetMembersNamed(method.Name, false).OfType<IMethodDefinition>())
                {
                    if (baseMethod.Visibility == TypeMemberVisibility.Private) continue;

                    if (IsAssembly(baseMethod) && !InSameUnit(baseMethod, method))
                        continue;

                    if (filter != null && !filter.Include(baseMethod.UnWrapMember()))
                        continue;

                    // NOTE: Do not check method.IsHiddenBySignature here. C# is *always* hide-by-signature regardless of the metadata flag.
                    //       Do not check return type here, C# hides based on parameter types alone.

                    if (SignaturesParametersAreEqual(method, baseMethod))
                    {
                        if (!method.IsGeneric && !baseMethod.IsGeneric)
                            return baseMethod;

                        if (method.GenericParameterCount == baseMethod.GenericParameterCount)
                            return baseMethod;
                    }
                }
            }
            return Dummy.Method;
        }

        public static ITypeDefinition GetHiddenBaseType(this ITypeDefinition type, ICciFilter filter = null)
        {
            if (!(type is INestedTypeDefinition nestedType))
            {
                return Dummy.Type;
            }

            ITypeDefinition containingType = nestedType.ContainingTypeDefinition;

            foreach (ITypeReference baseClassRef in containingType.GetAllBaseTypes())
            {
                ITypeDefinition baseClass = baseClassRef.ResolvedType;
                foreach (ITypeDefinition baseType in baseClass.GetMembersNamed(nestedType.Name, ignoreCase: false).OfType<ITypeDefinition>())
                {
                    if (baseType.GetVisibility() == TypeMemberVisibility.Private)
                    {
                        continue;
                    }

                    if (IsAssembly(baseType) && !InSameUnit(baseType, nestedType))
                        continue;

                    if (filter != null && !filter.Include(baseType))
                        continue;

                    return baseType;
                }
            }

            return Dummy.Type;
        }

        public static bool SignaturesParametersAreEqual(this ISignature sig1, ISignature sig2)
        {
            return IteratorHelper.EnumerablesAreEqual<IParameterTypeInformation>(sig1.Parameters, sig2.Parameters, new ParameterInformationComparer());
        }

        private static Regex s_isKeywordRegex;
        public static bool IsKeyword(string s)
        {
            if (s_isKeywordRegex == null)
                s_isKeywordRegex = new Regex("^(abstract|as|break|case|catch|checked|class|const|continue|default|delegate|do|else|enum|event|explicit|extern|finally|foreach|for|get|goto|if|implicit|interface|internal|in|is|lock|namespace|new|operator|out|override|params|partial|private|protected|public|readonly|ref|return|sealed|set|sizeof|stackalloc|static|struct|switch|this|throw|try|typeof|unchecked|unsafe|using|virtual|volatile|while|yield|bool|byte|char|decimal|double|fixed|float|int|long|object|sbyte|short|string|uint|ulong|ushort|void)$", RegexOptions.Compiled);

            return s_isKeywordRegex.IsMatch(s);
        }

        public static IMethodImplementation GetMethodImplementation(this IMethodDefinition method)
        {
            return ((ITypeDefinition)method.ContainingType).ExplicitImplementationOverrides.Where(mi => mi.ImplementingMethod.Equals(method)).FirstOrDefault();
        }

        public static object GetExplicitInterfaceMethodNullableAttributeArgument(this IMethodImplementation methodImplementation)
        {
            if (methodImplementation != null)
            {
                uint typeToken = ((IMetadataObjectWithToken)methodImplementation.ContainingType).TokenValue;
                string location = methodImplementation.ContainingType.Locations.FirstOrDefault()?.Document?.Location;
                if (location != null)
                    return methodImplementation.ImplementedMethod.ContainingType.GetInterfaceImplementationAttributeConstructorArgument(typeToken, "System.Runtime.CompilerServices.NullableAttribute", location, NullableConstructorArgumentParser);
            }

            return null;
        }

        public static string GetNameWithoutExplicitType(this ITypeDefinitionMember member)
        {
            string name = member.Name.Value;

            int index = name.LastIndexOf(".");

            if (index < 0)
                return name;

            return name.Substring(index + 1);
        }

        public static bool IsExtensionMethod(this IMethodDefinition method)
        {
            if (!method.IsStatic)
                return false;

            return method.Attributes.HasAttributeOfType("System.Runtime.CompilerServices.ExtensionAttribute");
        }

        public static bool IsEffectivelySealed(this ITypeDefinition type)
        {
            if (type.IsSealed)
                return true;

            if (type.IsInterface)
                return false;

            // Types with only private constructors are effectively sealed
            if (!type.Methods
                .Any(m =>
                    m.IsConstructor &&
                    !m.IsStaticConstructor &&
                    m.IsVisibleOutsideAssembly()))
                return true;

            return false;
        }

        public static bool IsValueTuple(this IGenericTypeInstanceReference genericType)
        {
            return genericType.GenericType.FullName().StartsWith("System.ValueTuple");
        }

        public static bool IsNullableValueType(this IGenericTypeInstanceReference genericType)
        {
            return genericType.GenericType.FullName().StartsWith("System.Nullable");
        }

        public static bool IsException(this ITypeDefinition type)
        {
            foreach (var baseTypeRef in type.GetBaseTypes())
            {
                if (baseTypeRef.AreEquivalent("System.Exception"))
                    return true;
            }
            return false;
        }

        public static bool IsAttribute(this ITypeDefinition type)
        {
            foreach (var baseTypeRef in type.GetBaseTypes())
            {
                if (baseTypeRef.AreEquivalent("System.Attribute"))
                    return true;
            }
            return false;
        }

        public static bool HasAttributeOfType(this IEnumerable<ICustomAttribute> attributes, string attributeName)
        {
            return GetAttributeOfType(attributes, attributeName) != null;
        }

        public static bool TryGetAttributeOfType(this IEnumerable<ICustomAttribute> attributes, string attributeName, out ICustomAttribute customAttribute)
        {
            customAttribute = attributes?.GetAttributeOfType(attributeName);
            return customAttribute != null;
        }

        private static ICustomAttribute GetAttributeOfType(this IEnumerable<ICustomAttribute> attributes, string attributeName)
        {
            return attributes.FirstOrDefault(a => a.Type.AreEquivalent(attributeName));
        }

        public static bool HasIsByRefLikeAttribute(this IEnumerable<ICustomAttribute> attributes)
        {
            return attributes.HasAttributeOfType("System.Runtime.CompilerServices.IsByRefLikeAttribute");
        }

        public static bool HasIsReadOnlyAttribute(this IEnumerable<ICustomAttribute> attributes)
        {
            return attributes.HasAttributeOfType("System.Runtime.CompilerServices.IsReadOnlyAttribute");
        }

        public static string[] GetValueTupleNames(this IEnumerable<ICustomAttribute> attributes)
        {
            string[] names = null;
            var attribute = attributes.GetAttributeOfType("System.Runtime.CompilerServices.TupleElementNamesAttribute");
            if (attribute != null && attribute.Arguments.Single() is IMetadataCreateArray createArray)
            {
                names = new string[createArray.Sizes.Single()];
                var i = 0;
                foreach (var argument in createArray.Initializers)
                {
                    if (argument is IMetadataConstant constant)
                    {
                        names[i] = (string)constant.Value;
                    }

                    i++;
                }
            }

            return names;
        }

        public static object GetInterfaceImplementationAttributeConstructorArgument(this ITypeReference interfaceImplementation, uint typeDefinitionToken, string attributeType, string assemblyPath, Func<CustomAttributeValue<string>, object> argumentResolver)
        {
            using (FileStream stream = File.OpenRead(assemblyPath))
            using (PEReader peFile = new PEReader(stream))
            {
                SRMetadataReader metadataReader = peFile.GetMetadataReader();
                int rowId = GetRowId(typeDefinitionToken);
                TypeDefinition typeDefinition = metadataReader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(rowId));

                uint interfaceImplementationToken = ((IMetadataObjectWithToken)interfaceImplementation).TokenValue;
                IEnumerable<InterfaceImplementation> foundInterfaces = typeDefinition.GetInterfaceImplementations().Select(metadataReader.GetInterfaceImplementation).Where(impl => metadataReader.GetToken(impl.Interface) == (int)interfaceImplementationToken);
                if (foundInterfaces.Any())
                {
                    InterfaceImplementation iImpl = foundInterfaces.First();
                    return GetCustomAttributeArgument(metadataReader, iImpl.GetCustomAttributes(), attributeType, argumentResolver);
                }
            }

            return null;
        }

        public static object GetGenericParameterConstraintConstructorArgument(this IGenericParameter parameter, int constraintIndex, string attributeType, string assemblyPath, Func<CustomAttributeValue<string>, object> argumentResolver)
        {
            using (FileStream stream = File.OpenRead(assemblyPath))
            using (PEReader peFile = new PEReader(stream))
            {
                SRMetadataReader metadataReader = peFile.GetMetadataReader();
                uint token = ((IMetadataObjectWithToken)parameter).TokenValue;
                int rowId = GetRowId(token);
                GenericParameter genericParameter = metadataReader.GetGenericParameter(MetadataTokens.GenericParameterHandle(rowId));
                GenericParameterConstraint constraint = metadataReader.GetGenericParameterConstraint(genericParameter.GetConstraints()[constraintIndex]);
                return GetCustomAttributeArgument(metadataReader, constraint.GetCustomAttributes(), attributeType, argumentResolver);
            }

        }

        private static object GetCustomAttributeArgument(SRMetadataReader metadataReader, CustomAttributeHandleCollection customAttributeHandles, string attributeType, Func<CustomAttributeValue<string>, object> argumentResolver)
        {
            foreach (CustomAttributeHandle customAttributeHandle in customAttributeHandles)
            {
                CustomAttribute customAttribute = metadataReader.GetCustomAttribute(customAttributeHandle);

                if (TryGetAttributeName(metadataReader, customAttribute, out StringHandle typeNamespaceHandle, out StringHandle typeNameHandle))
                {
                    string attributeTypeName = $"{metadataReader.GetString(typeNamespaceHandle)}.{metadataReader.GetString(typeNameHandle)}";
                    if (attributeType == attributeTypeName)
                    {
                        CustomAttributeValue<string> value = customAttribute.DecodeValue(new CustomAttributeTypeProvider());
                        return argumentResolver(value);
                    }
                }
            }

            return null;
        }

        private static int GetRowId(uint token)
        {
            const uint metadataRowIdMask = (1 << 24) - 1;
            return (int)(token & metadataRowIdMask);
        }

        private static bool TryGetAttributeName(SRMetadataReader reader, CustomAttribute attr, out StringHandle typeNamespaceHandle, out StringHandle typeNameHandle)
        {
            EntityHandle ctorHandle = attr.Constructor;
            switch (ctorHandle.Kind)
            {
                case HandleKind.MemberReference:
                    EntityHandle container = reader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                    if (container.Kind == HandleKind.TypeReference)
                    {
                        TypeReference tr = reader.GetTypeReference((TypeReferenceHandle)container);
                        typeNamespaceHandle = tr.Namespace;
                        typeNameHandle = tr.Name;
                        return true;
                    }
                    break;

                case HandleKind.MethodDefinition:
                    MethodDefinition md = reader.GetMethodDefinition((MethodDefinitionHandle)ctorHandle);
                    TypeDefinition td = reader.GetTypeDefinition(md.GetDeclaringType());
                    typeNamespaceHandle = td.Namespace;
                    typeNameHandle = td.Name;
                    return true;
            }

            // Unusual case, potentially invalid IL
            typeNamespaceHandle = default;
            typeNameHandle = default;
            return false;
        }

        private static IEnumerable<ITypeReference> GetBaseTypes(this ITypeReference typeRef)
        {
            ITypeDefinition type = typeRef.GetDefinitionOrNull();

            if (type == null)
                yield break;

            foreach (var baseTypeRef in type.BaseClasses)
            {
                yield return baseTypeRef;

                foreach (var nestedBaseTypeRef in GetBaseTypes(baseTypeRef))
                    yield return nestedBaseTypeRef;
            }
        }

        // Delegate to parse nullable attribute argument retrieved using System.Reflection.Metadata
        internal static readonly Func<CustomAttributeValue<string>, object> NullableConstructorArgumentParser = value =>
        {
            if (value.FixedArguments.IsEmpty)
            {
                throw new ArgumentException("NullableAttribute constructor should have only 1 fixed argument");
            }

            CustomAttributeTypedArgument<string> argument = value.FixedArguments[0];
            if (argument.Type == "uint8[]")
            {
                ImmutableArray<CustomAttributeTypedArgument<string>> argumentValue = (ImmutableArray<CustomAttributeTypedArgument<string>>)argument.Value;
                byte[] array = new byte[argumentValue.Length];
                for (int i = 0; i < argumentValue.Length; i++)
                {
                    array[i] = (byte)argumentValue[i].Value;
                }

                return array;
            }

            if (argument.Type == "uint8")
            {
                return argument.Value;
            }

            throw new ArgumentException("NullableAttribute should habe a byte or byte[] as a parameters");
        };
    }
}
