// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.SourceRewriter.Rewriters
{
    internal class PublicTypesToInternalRewriter : CSharpSyntaxRewriter
    {
        public PublicTypesToInternalRewriter()
            : base(false) { }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            return base.VisitStructDeclaration((StructDeclarationSyntax)ChangePublicDeclarationToInternal(node));
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            return base.VisitInterfaceDeclaration((InterfaceDeclarationSyntax)ChangePublicDeclarationToInternal(node));
        }

        public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            return base.VisitEnumDeclaration((EnumDeclarationSyntax)ChangePublicDeclarationToInternal(node));
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            return base.VisitClassDeclaration((ClassDeclarationSyntax)ChangePublicDeclarationToInternal(node));
        }

        private SyntaxNode ChangePublicDeclarationToInternal(BaseTypeDeclarationSyntax node)
        {
            if (HasPublicModifier(node, out SyntaxToken publicToken))
            {
                return node.ReplaceToken(publicToken, SyntaxFactory.Token(SyntaxKind.InternalKeyword)
                                                                    .WithLeadingTrivia(publicToken.LeadingTrivia)
                                                                    .WithTrailingTrivia(publicToken.TrailingTrivia));
            }
            else
            {
                return node;
            }
        }

        private bool HasPublicModifier(BaseTypeDeclarationSyntax token, out SyntaxToken publicToken)
        {
            foreach (SyntaxToken modifier in token.Modifiers)
            {
                if (modifier.Text == "public")
                {
                    publicToken = modifier;
                    return true;
                }
            }
            publicToken = default(SyntaxToken);
            return false;
        }
    }
}
