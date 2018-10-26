// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.DotNet.CodeAnalysis.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ResourceUsageAnalyzer : BaseAnalyzer
    {
        private const string Title = "Invalid SR.Format call";
        private const string Description = "The SR.Format call should be removed.";
        private const string AnalyzerName = "ResourceUsageAnalyzer";

        private static DiagnosticDescriptor InvalidSRFormatCall = new DiagnosticDescriptor(DiagnosticIds.BCL0020.ToString(), Title, "", AnalyzerName, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(InvalidSRFormatCall); } }

        INamedTypeSymbol SRSymbol { get; set; }

        public override void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            SRSymbol = context.Compilation.GetTypeByMetadataName("System.SR");
            if (SRSymbol != null)
            {
                context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
            }
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            InvocationExpressionSyntax invokeExpr = context.Node as InvocationExpressionSyntax;

            if (invokeExpr == null) return;

            MemberAccessExpressionSyntax memberAccessExpr = invokeExpr.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpr == null) return;

            IMethodSymbol memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;
            if (memberSymbol == null) return;

            if (memberSymbol.Name.Equals("Format") && memberSymbol.ContainingType.Equals(SRSymbol))
            {
                bool isValidCall = false;
                if (memberSymbol.Parameters.Length > 1
                    && memberSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String)
                {
                    isValidCall = true;
                }

                if (!isValidCall)
                    context.ReportDiagnostic(Diagnostic.Create(InvalidSRFormatCall, invokeExpr.GetLocation(), invokeExpr.GetText()));
            }
        }
    }
}
