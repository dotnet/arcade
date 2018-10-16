// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.ApiVersioning.Analyzers
{
    internal class DoNotUseUnversionedTypesInVersionedApisAnalyzerImplementation
    {
        private readonly KnownTypes _knownTypes;
        private readonly TypeVersionChecker _typeVersionChecker;
        private SemanticModelAnalysisContext _context;

        public DoNotUseUnversionedTypesInVersionedApisAnalyzerImplementation(
            SemanticModelAnalysisContext context,
            KnownTypes knownTypes,
            TypeVersionChecker typeVersionChecker)
        {
            _context = context;
            _knownTypes = knownTypes;
            _typeVersionChecker = typeVersionChecker;
        }

        public void Run()
        {
            try
            {
                SyntaxTree tree = _context.SemanticModel.SyntaxTree;
                new Visitor(_context, _knownTypes, _typeVersionChecker).Visit(tree.GetRoot());
            }
            catch (Exception ex)
            {
                _context.ReportDiagnostic(
                    Diagnostic.Create(DoNotUseUnversionedTypesInVersionedApis.Error, null, ex.GetType(), ex.Message));
            }
        }

        internal class Visitor : CSharpSyntaxWalker
        {
            private readonly TypeVersionChecker _typeVersionChecker;
            private SemanticModelAnalysisContext _context;

            public Visitor(
                SemanticModelAnalysisContext context,
                KnownTypes knownTypes,
                TypeVersionChecker typeVersionChecker)
            {
                _context = context;
                SemanticModel = context.SemanticModel;
                Compilation = SemanticModel.Compilation;
                KnownTypes = knownTypes;
                _typeVersionChecker = typeVersionChecker;
            }

            private SemanticModel SemanticModel { get; }
            private Compilation Compilation { get; }
            private KnownTypes KnownTypes { get; }

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                IImmutableList<ITypeSymbol> baseTypes = SemanticModel.GetAllBaseTypeSymbols(node);
                bool isController = baseTypes.Any(t => t.Equals(KnownTypes.Controller));
                if (!isController)
                {
                    return;
                }

                IImmutableList<AttributeUsageInfo> attributeInfo = SemanticModel.GetAttributeInfo(node.AttributeLists);
                bool isVersioned = attributeInfo.Any(info => info.AttributeType.Equals(KnownTypes.ApiVersionAttribute));
                if (!isVersioned)
                {
                    return;
                }

                base.VisitClassDeclaration(node);
            }

            private IEnumerable<(ITypeSymbol symbol, TypeSyntax syntax)> GetContractTypes(
                MethodDeclarationSyntax method)
            {
                yield return (SemanticModel.GetTypeSymbol(method.ReturnType), method.ReturnType);

                foreach ((ITypeSymbol type, IImmutableList<AttributeParameterInfo> parameters) in SemanticModel
                    .GetAttributeInfo(method.AttributeLists))
                {
                    if (!SemanticModel.IsAssignableTo(type, KnownTypes.ProducesResponseTypeAttribute))
                    {
                        continue;
                    }

                    List<(ExpressionSyntax expression, ITypeSymbol type)> typeParameters =
                        parameters.Select(
                                p => (expression: p.Expression, type: SemanticModel.GetTypeSymbol(p.Expression)))
                            .Where(p => p.type.Equals(KnownTypes.Type))
                            .ToList();
                    if (typeParameters.Count == 0)
                    {
                        continue;
                    }

                    (ExpressionSyntax expression, ITypeSymbol type) typeParameter = typeParameters[0];
                    var visitor = new GetTypeOfExpressionSymbolVisitor();
                    visitor.Visit(typeParameter.expression);
                    yield return (SemanticModel.GetTypeSymbol(visitor.Type), visitor.Type);
                }

                foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
                {
                    yield return (SemanticModel.GetTypeSymbol(parameter.Type), parameter.Type);
                }
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                if (node.Modifiers.All(mod => mod.ValueText != "public"))
                {
                    return;
                }

                IEnumerable<(ITypeSymbol symbol, TypeSyntax syntax)> contractTypes = GetContractTypes(node);
                IEnumerable<(ITypeSymbol symbol, Location location)> typeUsages =
                    SemanticModel.GetTypeUsages(contractTypes);

                foreach ((ITypeSymbol symbol, Location location) in typeUsages)
                {
                    if (!_typeVersionChecker.IsVersionable(symbol))
                    {
                        _context.ReportDiagnostic(
                            Diagnostic.Create(
                                DoNotUseUnversionedTypesInVersionedApis.Rule,
                                location,
                                symbol.ToDisplayString()));
                    }
                }
            }
        }
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [PublicAPI]
    public sealed class DoNotUseUnversionedTypesInVersionedApis : DiagnosticAnalyzer
    {
        private const string RuleId = "API0001";

        private const string ErrorId = "ERR00";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            RuleId,
            "Versioned API methods should not expose non-versioned types.",
            "Type {0} is not versioned.",
            "Correctness",
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor Error = new DiagnosticDescriptor(
            ErrorId,
            "Unexpected Error",
            "Unexpected Error of type '{0}' with message '{1}'",
            "InternalError",
            DiagnosticSeverity.Error,
            true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Rule, Error);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterCompilationStartAction(CompilationStart);
        }

        private void CompilationStart(CompilationStartAnalysisContext context)
        {
            var knownTypes = new KnownTypes(context.Compilation);
            if (knownTypes.HaveRequired())
            {
                context.RegisterSemanticModelAction(
                    ctx =>
                    {
                        new DoNotUseUnversionedTypesInVersionedApisAnalyzerImplementation(
                            ctx,
                            knownTypes,
                            new TypeVersionChecker(context.Compilation)).Run();
                    });
            }
        }
    }

    internal class GetTypeOfExpressionSymbolVisitor : CSharpSyntaxWalker
    {
        public TypeSyntax Type { get; private set; }

        public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            Type = node.Type;
        }
    }
}
