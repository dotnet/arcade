// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Cci;
using Microsoft.Cci.Comparers;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Writers;
using Microsoft.Cci.Writers.CSharp;
using Microsoft.Cci.Writers.Syntax;
using Microsoft.Fx.Progress;

namespace Microsoft.Fx.ApiReviews.Differencing.Exporters
{
    public static class ReferenceAssemblyExporter
    {
        public static void Export(IProgressMonitor progressMonitor,
                                  IEnumerable<IAssembly> assemblies,
                                  string path,
                                  bool oneFilePerNamespace = false,
                                  bool includeAssemblyAttributes = false)
        {
            progressMonitor.SetTask("Export");

            var filter = new ReferenceAssemblyFilter();

            if (!oneFilePerNamespace)
            {
                WriteAssemblies(progressMonitor, assemblies, path, null, filter, writeAssemblyAttributes: includeAssemblyAttributes);
            }
            else
            {
                var namespaces = assemblies.SelectMany(a => a.GetAllTypes())
                                           .Where(t => filter.Include(t) && !(t is INestedTypeDefinition))
                                           .Select(t => t.GetNamespaceName())
                                           .Distinct()
                                           .ToArray();

                foreach (var @namespace in namespaces)
                {
                    var namespaceFileName = Path.Combine(path, @namespace + ".cs");
                    WriteAssemblies(progressMonitor, assemblies, namespaceFileName, @namespace, filter, writeAssemblyAttributes: false);
                }
            }
        }

        private static void WriteAssemblies(IProgressMonitor progressMonitor, IEnumerable<IAssembly> assemblies, string fileName, string @namespace, ICciFilter filter, bool writeAssemblyAttributes)
        {
            progressMonitor.SetDetails("Writing to {0}...", Path.GetFileName(fileName));

            using (var textWriter = new StreamWriter(fileName))
            {
                var syntaxWriter = new TextSyntaxWriter(textWriter);
                syntaxWriter.SpacesInIndent = 4;

                var writer = new CSharpWriter(syntaxWriter, filter, false, writeAssemblyAttributes)
                {
                    IncludeSpaceBetweenMemberGroups = false,
                    IncludeMemberGroupHeadings = false,
                    PutBraceOnNewLine = true,
                    HighlightBaseMembers = false,
                    HighlightInterfaceMembers = false,
                    LangVersion = CSDeclarationWriter.LangVersionPreview,
                };

                var namespaceGroups = assemblies.SelectMany(a => a.GetAllTypes())
                                                .Where(t => filter.Include(t) && !(t is INestedTypeDefinition))
                                                .GroupBy(t => t.GetNamespaceName());

                foreach (var g in namespaceGroups)
                {
                    if (@namespace != null && @namespace != g.Key)
                        continue;

                    var ns = g.Key;
                    var orderedTypes = g.OrderBy(t => t, new TypeDefinitionComparer());

                    if (ns != null && string.IsNullOrEmpty(ns))
                    {
                        foreach (var type in orderedTypes)
                        {
                            writer.Visit(type);
                        }
                    }
                    else
                    {
                        writer.SyntaxWriter.WriteKeyword("namespace");
                        writer.SyntaxWriter.WriteSpace();
                        writer.SyntaxWriter.WriteIdentifier(ns);

                        using (writer.SyntaxWriter.StartBraceBlock(writer.PutBraceOnNewLine))
                        {
                            foreach (var type in orderedTypes)
                            {
                                writer.Visit(type);
                            }
                        }
                    }

                    writer.SyntaxWriter.WriteLine();
                }
            }
        }

        private sealed class ReferenceAssemblyFilter : PublicOnlyCciFilter
        {
            public ReferenceAssemblyFilter()
                : base(excludeAttributes: false)
            {
            }

            private bool IsExcludedAttribute(string name)
            {
                switch (name)
                {
                    case "System.Diagnostics.DebuggerHiddenAttribute":
                    case "System.Diagnostics.DebuggerStepThroughAttribute":
                    case "System.Runtime.CompilerServices.CompilerGeneratedAttribute":
                    case "System.Runtime.InteropServices.ClassInterfaceAttribute":
                    case "System.Runtime.InteropServices.ComDefaultInterfaceAttribute":
                    case "System.Runtime.InteropServices.ComVisibleAttribute":
                    case "System.Security.Permissions.PermissionSetAttribute":
                    case "System.Security.Permissions.SecurityPermissionAttribute":
                    case "System.Security.SecurityCriticalAttribute":
                    case "System.Security.SecuritySafeCriticalAttribute":
                        return true;
                    default:
                        return false;
                }
            }

            public override bool Include(ICustomAttribute attribute)
            {
                if (IsExcludedAttribute(attribute.Type.FullName()))
                    return false;

                return base.Include(attribute);
            }

            public override bool Include(ITypeDefinitionMember member)
            {
                if (member is IMethodDefinition method &&
                    method.IsOverride() &&
                    method.IsDestructor())
                {
                    var type = method.ContainingTypeDefinition;
                    var canExtendType = !type.IsSealed &&
                                         type.Methods.Any(m => m.IsConstructor && m.IsVisibleOutsideAssembly());

                    if (!canExtendType)
                        return false;
                }

                return base.Include(member);
            }
        }
    }
}
