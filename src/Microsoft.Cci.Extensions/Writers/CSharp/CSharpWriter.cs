// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Traversers;
using Microsoft.Cci.Writers.CSharp;
using Microsoft.Cci.Writers.Syntax;

namespace Microsoft.Cci.Writers
{
    public class CSharpWriter : SimpleTypeMemberTraverser, ICciWriter
    {
        private readonly ISyntaxWriter _syntaxWriter;
        private readonly IStyleSyntaxWriter _styleWriter;
        private readonly CSDeclarationWriter _declarationWriter;
        private readonly bool _writeAssemblyAttributes;
        private readonly bool _apiOnly;
        private readonly ICciFilter _cciFilter;

        private bool _firstMemberGroup;

        public CSharpWriter(ISyntaxWriter writer, ICciFilter filter, bool apiOnly, bool writeAssemblyAttributes = false)
            : base(filter)
        {
            _syntaxWriter = writer;
            _styleWriter = writer as IStyleSyntaxWriter;
            _apiOnly = apiOnly;
            _cciFilter = filter;
            _declarationWriter = new CSDeclarationWriter(_syntaxWriter, filter, !apiOnly);
            _writeAssemblyAttributes = writeAssemblyAttributes;
        }

        public ISyntaxWriter SyntaxWriter { get { return _syntaxWriter; } }

        public ICciDeclarationWriter DeclarationWriter { get { return _declarationWriter; } }

        public bool IncludeSpaceBetweenMemberGroups { get; set; }

        public bool IncludeMemberGroupHeadings { get; set; }

        public bool HighlightBaseMembers { get; set; }

        public bool HighlightInterfaceMembers { get; set; }

        public bool PutBraceOnNewLine { get; set; }

        public bool IncludeGlobalPrefixForCompilation
        {
            get { return _declarationWriter.ForCompilationIncludeGlobalPrefix; }
            set { _declarationWriter.ForCompilationIncludeGlobalPrefix = value; }
        }

        public string PlatformNotSupportedExceptionMessage
        {
            get { return _declarationWriter.PlatformNotSupportedExceptionMessage; }
            set { _declarationWriter.PlatformNotSupportedExceptionMessage = value; }
        }

        public bool AlwaysIncludeBase
        {
            get { return _declarationWriter.AlwaysIncludeBase; }
            set { _declarationWriter.AlwaysIncludeBase = value; }
        }

        public Version LangVersion
        {
            get { return _declarationWriter.LangVersion; }
            set { _declarationWriter.LangVersion = value; }
        }

        public void WriteAssemblies(IEnumerable<IAssembly> assemblies)
        {
            foreach (var assembly in assemblies)
                Visit(assembly);
        }

        public override void Visit(IAssembly assembly)
        {
            if (_writeAssemblyAttributes)
            {
                _declarationWriter.WriteDeclaration(assembly);
            }

            base.Visit(assembly);
        }

        public override void Visit(INamespaceDefinition ns)
        {
            if (ns != null && string.IsNullOrEmpty(ns.Name.Value))
            {
                base.Visit(ns);
            }
            else
            {
                _declarationWriter.WriteDeclaration(ns);

                using (_syntaxWriter.StartBraceBlock(PutBraceOnNewLine))
                {
                    base.Visit(ns);
                }
            }

            _syntaxWriter.WriteLine();
        }

        public override void Visit(IEnumerable<ITypeDefinition> types)
        {
            WriteMemberGroupHeader(types.FirstOrDefault(Filter.Include) as ITypeDefinitionMember);
            base.Visit(types);
        }

        public override void Visit(ITypeDefinition type)
        {
            _declarationWriter.WriteDeclaration(type);

            if (!type.IsDelegate)
            {
                using (_syntaxWriter.StartBraceBlock(PutBraceOnNewLine))
                {
                    // If we have no constructors then output a private one this
                    // prevents the C# compiler from creating a default public one.
                    var constructors = type.Methods.Where(m => m.IsConstructor && Filter.Include(m));
                    if (!type.IsStatic && !constructors.Any())
                    {
                        // HACK... this will likely not work for any thing other than CSDeclarationWriter
                        _declarationWriter.WriteDeclaration(CSDeclarationWriter.GetDummyConstructor(type));
                        _syntaxWriter.WriteLine();
                    }

                    _firstMemberGroup = true;
                    base.Visit(type);
                }
            }
            _syntaxWriter.WriteLine();
        }

        public override void Visit(IEnumerable<ITypeDefinitionMember> members)
        {
            WriteMemberGroupHeader(members.FirstOrDefault(Filter.Include));
            base.Visit(members);
        }

        public override void Visit(ITypeDefinition parentType, IEnumerable<IFieldDefinition> fields)
        {
            if (parentType.IsStruct && !_apiOnly)
            {
                // For compile-time compat, the following rules should work for producing a reference assembly. We drop all private fields, but add back certain synthesized private fields for a value type (struct) as follows:
                // - If there are any private fields that are or contain any value type members, add a single private field of type int.
                // - And, if there are any private fields that are or contain any reference type members, add a single private field of type object.
                // - And, if the type is generic, then for every type parameter of the type, if there are any private fields that are or contain any members whose type is that type parameter, we add a direct private field of that type.

                // Note: By "private", we mean not visible outside the assembly.

                // For more details see issue https://github.com/dotnet/corefx/issues/6185 
                // this blog is helpful as well http://blog.paranoidcoding.com/2016/02/15/are-private-members-api-surface.html

                List<IFieldDefinition> newFields = new List<IFieldDefinition>();
                var includedVisibleFields = fields.Where(f => f.IsVisibleOutsideAssembly()).Where(_cciFilter.Include);
                includedVisibleFields = includedVisibleFields.OrderBy(GetMemberKey, StringComparer.OrdinalIgnoreCase);

                var excludedFields = fields.Except(includedVisibleFields).Where(f => !f.IsStatic);

                if (excludedFields.Any())
                {
                    var genericTypedFields = excludedFields.Where(f => f.Type.UnWrap().IsGenericParameter());

                    // Compiler needs to see any fields, even private, that have generic arguments to be able
                    // to validate there aren't any struct layout cycles
                    foreach (var genericField in genericTypedFields)
                        newFields.Add(genericField);

                    // For definiteassignment checks the compiler needs to know there is a private field
                    // that has not been initialized so if there are any we need to add a dummy private
                    // field to help the compiler do its job and error about uninitialized structs
                    bool hasRefPrivateField = excludedFields.Any(f => f.Type.IsOrContainsReferenceType());

                    // If at least one of the private fields contains a reference type then we need to
                    // set this field type to object or reference field to inform the compiler to block
                    // taking pointers to this struct because the GC will not track updating those references
                    if (hasRefPrivateField)
                    {
                        IFieldDefinition fieldType = DummyFieldWriterHelper(parentType, excludedFields, parentType.PlatformType.SystemObject);
                        newFields.Add(fieldType);
                    }

                    bool hasValueTypePrivateField = excludedFields.Any(f => !f.Type.IsOrContainsReferenceType());

                    if (hasValueTypePrivateField)
                    {
                        IFieldDefinition fieldType = DummyFieldWriterHelper(parentType, excludedFields, parentType.PlatformType.SystemInt32, "_dummyPrimitive");
                        newFields.Add(fieldType);
                    }
                }

                foreach (var visibleField in includedVisibleFields)
                    newFields.Add(visibleField);

                foreach (var field in newFields)
                    Visit(field);
            }
            else
            {
                base.Visit(parentType, fields);
            }
        }

        private IFieldDefinition DummyFieldWriterHelper(ITypeDefinition parentType, IEnumerable<IFieldDefinition> excludedFields, ITypeReference fieldType, string fieldName = "_dummy")
        {
            // For primitive types that have a field of their type set the dummy field to that type
            if (excludedFields.Count() == 1)
            {
                var onlyField = excludedFields.First();

                if (TypeHelper.TypesAreEquivalent(onlyField.Type, parentType))
                {
                    fieldType = parentType;
                }
            }

            return new DummyPrivateField(parentType, fieldType, fieldName);
        }

        public override void Visit(ITypeDefinitionMember member)
        {
            IDisposable style = null;

            if (_styleWriter != null)
            {
                // Favor overrides over interface implementations (i.e. consider override Dispose() as an override and not an interface implementation)
                if (this.HighlightBaseMembers && member.IsOverride())
                    style = _styleWriter.StartStyle(SyntaxStyle.InheritedMember);
                else if (this.HighlightInterfaceMembers && member.IsInterfaceImplementation())
                    style = _styleWriter.StartStyle(SyntaxStyle.InterfaceMember);
            }

            _declarationWriter.WriteDeclaration(member);

            if (style != null)
                style.Dispose();

            _syntaxWriter.WriteLine();
            base.Visit(member);
        }

        private void WriteMemberGroupHeader(ITypeDefinitionMember member)
        {
            if (IncludeMemberGroupHeadings || IncludeSpaceBetweenMemberGroups)
            {
                string header = CSharpWriter.MemberGroupHeading(member);

                if (header != null)
                {
                    if (IncludeSpaceBetweenMemberGroups)
                    {
                        if (!_firstMemberGroup)
                            _syntaxWriter.WriteLine(true);
                        _firstMemberGroup = false;
                    }

                    if (IncludeMemberGroupHeadings)
                    {
                        IDisposable dispose = null;
                        if (_styleWriter != null)
                            dispose = _styleWriter.StartStyle(SyntaxStyle.Comment);

                        _syntaxWriter.Write("// {0}", header);

                        if (dispose != null)
                            dispose.Dispose();
                        _syntaxWriter.WriteLine();
                    }
                }
            }
        }

        public static string MemberGroupHeading(ITypeDefinitionMember member)
        {
            if (member == null)
                return null;

            IMethodDefinition method = member as IMethodDefinition;
            if (method != null)
            {
                if (method.IsConstructor)
                    return "Constructors";

                return "Methods";
            }

            IFieldDefinition field = member as IFieldDefinition;
            if (field != null)
                return "Fields";

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
                return "Properties";

            IEventDefinition evnt = member as IEventDefinition;
            if (evnt != null)
                return "Events";

            INestedTypeDefinition nType = member as INestedTypeDefinition;
            if (nType != null)
                return "Nested Types";

            return null;
        }
    }
}
