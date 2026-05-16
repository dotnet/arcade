// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.DotNet.Build.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.GenFacades
{
    /// <summary>
    /// Generates a not-supported assembly from reference sources, optionally enriching
    /// XML documentation comments from implementation sources in the same project.
    /// </summary>
    public class NotSupportedAssemblyGenerator : RoslynBuildTask
    {
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        [Required]
        public string Message { get; set; }

        public string LangVersion { get; set; }

        public string ApiExclusionListPath { get; set; }

        /// <summary>
        /// Optional implementation source files (e.g. <c>**/*.cs</c> for the current project)
        /// used to provide XML doc comments for the generated not-supported stubs.
        /// Warnings are emitted for public APIs that cannot be located or lack documentation.
        /// </summary>
        public ITaskItem[] ImplementationSourceFiles { get; set; }

        public override bool ExecuteCore()
        {
            if (SourceFiles == null || SourceFiles.Length == 0)
            {
                Log.LogError("There are no source files.");
                return false;
            }

            GenerateNotSupportedAssemblyFiles(SourceFiles);

            return !Log.HasLoggedErrors;
        }

        private void GenerateNotSupportedAssemblyFiles(IEnumerable<ITaskItem> sourceFiles)
        {
            string[] apiExclusions = null;
            if (!string.IsNullOrEmpty(ApiExclusionListPath) && File.Exists(ApiExclusionListPath))
            {
                apiExclusions = File.ReadAllLines(ApiExclusionListPath);
            }

            LanguageVersion languageVersion = LanguageVersion.Default;
            if (!string.IsNullOrEmpty(LangVersion) && !LanguageVersionFacts.TryParse(LangVersion, out languageVersion))
            {
                Log.LogError($"Invalid LangVersion value '{LangVersion}'");
                return;
            }

            DocCommentIndex docIndex = BuildDocCommentIndex(languageVersion);

            foreach (ITaskItem item in sourceFiles)
            {
                string sourceFile = item.ItemSpec;
                string outputPath = item.GetMetadata("OutputPath");

                if (!File.Exists(sourceFile))
                {
                    Log.LogError($"File {sourceFile} was not found.");
                    continue;
                }

                GenerateNotSupportedAssemblyForSourceFile(sourceFile, outputPath, apiExclusions, languageVersion, docIndex);
            }
        }

        private DocCommentIndex BuildDocCommentIndex(LanguageVersion languageVersion)
        {
            if (ImplementationSourceFiles == null || ImplementationSourceFiles.Length == 0)
                return null;

            var parseOptions = new CSharpParseOptions(languageVersion);
            var index = new DocCommentIndex();

            foreach (ITaskItem item in ImplementationSourceFiles)
            {
                string path = item.ItemSpec;
                if (!File.Exists(path))
                    continue;

                try
                {
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions);
                    index.AddSourceTree(tree);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Failed to parse implementation source '{path}': {ex.Message}");
                }
            }

            return index;
        }

        private void GenerateNotSupportedAssemblyForSourceFile(
            string sourceFile,
            string outputPath,
            string[] apiExclusions,
            LanguageVersion languageVersion,
            DocCommentIndex docIndex)
        {
            SyntaxTree syntaxTree;

            try
            {
                syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile), new CSharpParseOptions(languageVersion));
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, false);
                return;
            }

            var rewriter = new NotSupportedAssemblyRewriter(Message, apiExclusions, docIndex);
            SyntaxNode root = rewriter.Visit(syntaxTree.GetRoot());
            string text = root.GetText().ToString();
            File.WriteAllText(outputPath, text);

            if (docIndex != null)
            {
                foreach (string api in rewriter.ApisNotFoundInImplementation)
                    Log.LogWarning($"Public API '{api}' could not be located in implementation sources.");

                foreach (string api in rewriter.ApisMissingDocumentation)
                    Log.LogWarning($"Public API '{api}' is missing documentation in implementation sources.");
            }
        }
    }

    /// <summary>
    /// Builds a lookup from public API keys to their XML doc comment trivia,
    /// collected from one or more implementation source files.  First occurrence wins.
    /// </summary>
    internal sealed class DocCommentIndex
    {
        // All APIs seen in implementation sources (with or without docs).
        private readonly HashSet<string> _seenApis = new(StringComparer.Ordinal);
        // APIs that have at least one XML doc comment (first occurrence wins).
        private readonly Dictionary<string, SyntaxTriviaList> _docTrivia = new(StringComparer.Ordinal);

        public void AddSourceTree(SyntaxTree tree)
        {
            var walker = new DocCommentWalker(this);
            walker.Visit(tree.GetRoot());
        }

        internal void RecordMember(string key, SyntaxTriviaList leading)
        {
            _seenApis.Add(key);

            if (!_docTrivia.ContainsKey(key))
            {
                var docTrivia = leading
                    .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                    .ToList();

                if (docTrivia.Count > 0)
                    _docTrivia[key] = SyntaxFactory.TriviaList(docTrivia);
            }
        }

        public bool HasMember(string key) => _seenApis.Contains(key);

        public bool TryGetDocComment(string key, out SyntaxTriviaList docTrivia)
            => _docTrivia.TryGetValue(key, out docTrivia);
    }

    /// <summary>
    /// Walks an implementation source tree and records doc comment trivia for all declared members.
    /// </summary>
    internal sealed class DocCommentWalker : CSharpSyntaxWalker
    {
        private readonly DocCommentIndex _index;

        public DocCommentWalker(DocCommentIndex index) { _index = index; }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            _index.RecordMember(ApiKey.GetTypeKey(node), node.GetLeadingTrivia());
            base.VisitClassDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            _index.RecordMember(ApiKey.GetTypeKey(node), node.GetLeadingTrivia());
            base.VisitStructDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            _index.RecordMember(ApiKey.GetTypeKey(node), node.GetLeadingTrivia());
            base.VisitInterfaceDeclaration(node);
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            _index.RecordMember(ApiKey.GetTypeKey(node), node.GetLeadingTrivia());
            base.VisitRecordDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            _index.RecordMember(ApiKey.GetEnumKey(node), node.GetLeadingTrivia());
            base.VisitEnumDeclaration(node);
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            _index.RecordMember(ApiKey.GetDelegateKey(node), node.GetLeadingTrivia());
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
                _index.RecordMember(ApiKey.GetMemberKey(parent, ".ctor"), node.GetLeadingTrivia());
        }

        public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
                _index.RecordMember(ApiKey.GetMemberKey(parent, "Finalize"), node.GetLeadingTrivia());
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
                _index.RecordMember(ApiKey.GetMemberKey(parent, node.Identifier.ValueText), node.GetLeadingTrivia());
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
                _index.RecordMember(ApiKey.GetMemberKey(parent, node.Identifier.ValueText), node.GetLeadingTrivia());
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
                _index.RecordMember(ApiKey.GetMemberKey(parent, node.Identifier.ValueText), node.GetLeadingTrivia());
        }

        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
            {
                foreach (VariableDeclaratorSyntax v in node.Declaration.Variables)
                    _index.RecordMember(ApiKey.GetMemberKey(parent, v.Identifier.ValueText), node.GetLeadingTrivia());
            }
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
            {
                foreach (VariableDeclaratorSyntax v in node.Declaration.Variables)
                    _index.RecordMember(ApiKey.GetMemberKey(parent, v.Identifier.ValueText), node.GetLeadingTrivia());
            }
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
                _index.RecordMember(ApiKey.GetOperatorKey(parent, node.OperatorToken.ValueText), node.GetLeadingTrivia());
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            if (node.Parent is TypeDeclarationSyntax parent)
                _index.RecordMember(ApiKey.GetConversionOperatorKey(parent, node.ImplicitOrExplicitKeyword.ValueText), node.GetLeadingTrivia());
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            if (node.Parent is EnumDeclarationSyntax parentEnum)
                _index.RecordMember(ApiKey.GetEnumKey(parentEnum) + "." + node.Identifier.ValueText, node.GetLeadingTrivia());
        }
    }

    /// <summary>
    /// Helpers for computing consistent, fully-qualified API keys from syntax nodes.
    /// Handles regular namespaces, file-scoped namespaces, nested types, and top-level types.
    /// </summary>
    internal static class ApiKey
    {
        public static string GetTypeKey(TypeDeclarationSyntax node)
            => Qualify(GetParentPrefix(node.Parent), node.Identifier.ValueText.Trim());

        public static string GetEnumKey(EnumDeclarationSyntax node)
            => Qualify(GetParentPrefix(node.Parent), node.Identifier.ValueText.Trim());

        public static string GetDelegateKey(DelegateDeclarationSyntax node)
            => Qualify(GetParentPrefix(node.Parent), node.Identifier.ValueText.Trim());

        public static string GetMemberKey(TypeDeclarationSyntax parent, string memberName)
            => GetTypeKey(parent) + "." + memberName;

        public static string GetOperatorKey(TypeDeclarationSyntax parent, string operatorToken)
            => GetTypeKey(parent) + ".op_" + operatorToken;

        public static string GetConversionOperatorKey(TypeDeclarationSyntax parent, string implicitOrExplicit)
            => GetTypeKey(parent) + "." + implicitOrExplicit + " operator";

        private static string GetParentPrefix(SyntaxNode parent) => parent switch
        {
            NamespaceDeclarationSyntax ns => ns.Name.ToFullString().Trim(),
            FileScopedNamespaceDeclarationSyntax fns => fns.Name.ToFullString().Trim(),
            TypeDeclarationSyntax type => GetTypeKey(type),
            _ => string.Empty,
        };

        private static string Qualify(string prefix, string name)
            => string.IsNullOrEmpty(prefix) ? name : prefix + "." + name;
    }

    internal class NotSupportedAssemblyRewriter : CSharpSyntaxRewriter
    {
        private readonly string _message;
        private readonly IEnumerable<string> _exclusionApis;
        private readonly DocCommentIndex _docIndex;
        private readonly List<string> _apisNotFoundInImplementation = new();
        private readonly List<string> _apisMissingDocs = new();

        public IReadOnlyList<string> ApisNotFoundInImplementation => _apisNotFoundInImplementation;
        public IReadOnlyList<string> ApisMissingDocumentation => _apisMissingDocs;

        public NotSupportedAssemblyRewriter(string message, string[] exclusionApis, DocCommentIndex docIndex = null)
        {
            _message = message != null && message.StartsWith("SR.") ? "System." + message : message;
            _exclusionApis = exclusionApis?.Select(t => t.Substring(t.IndexOf(':') + 1));
            _docIndex = docIndex;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            string key = ApiKey.GetTypeKey(node);
            if (_exclusionApis != null && _exclusionApis.Contains(key))
                return null;

            SyntaxNode result = base.VisitClassDeclaration(node);
            return ApplyDocComment(result, key);
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Abstract/extern methods and interface members have neither body nor expression body.
            if (node.Body == null && node.ExpressionBody == null)
                return node;

            string key = ApiKey.GetMemberKey((TypeDeclarationSyntax)node.Parent, node.Identifier.ValueText);
            if (_exclusionApis != null && _exclusionApis.Contains(key))
                return null;

            BlockSyntax block = node.Identifier.ValueText == "Dispose" || node.Identifier.ValueText == "Finalize"
                ? CreateEmptyBlock()
                : CreateThrowBlock();
            SyntaxNode result = node.WithBody(block).WithExpressionBody(null).WithSemicolonToken(default);
            return ApplyDocComment(result, key);
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            string key = ApiKey.GetMemberKey((TypeDeclarationSyntax)node.Parent, node.Identifier.ValueText);
            if (_exclusionApis != null && _exclusionApis.Contains(key))
                return null;

            SyntaxNode result;

            // Handle expression-bodied properties (e.g., `public int X => _x;`).
            // Convert them to a getter-only property that throws.
            if (node.ExpressionBody != null)
            {
                var getterAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithBody(CreateThrowBlock());
                var accessorList = SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getterAccessor));
                result = node.WithExpressionBody(null)
                             .WithSemicolonToken(default)
                             .WithInitializer(null)
                             .WithAccessorList(accessorList);
            }
            else
            {
                // Strip property initializers (e.g., `public int X { get; } = 5;`).
                if (node.Initializer != null)
                    node = node.WithInitializer(null).WithSemicolonToken(default);

                result = base.VisitPropertyDeclaration(node);
            }

            return ApplyDocComment(result, key);
        }

        public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax node)
        {
            string key = ApiKey.GetMemberKey((TypeDeclarationSyntax)node.Parent, node.Identifier.ValueText);
            if (_exclusionApis != null && _exclusionApis.Contains(key))
                return null;

            SyntaxNode result = base.VisitEventDeclaration(node);
            return ApplyDocComment(result, key);
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            string key = ApiKey.GetMemberKey((TypeDeclarationSyntax)node.Parent, ".ctor");
            SyntaxNode result = node.WithBody(CreateThrowBlock()).WithExpressionBody(null).WithSemicolonToken(default);
            return ApplyDocComment(result, key);
        }

        public override SyntaxNode VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            return node.WithBody(CreateEmptyBlock()).WithExpressionBody(null).WithSemicolonToken(default);
        }

        public override SyntaxNode VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            // Auto-accessors and interface accessors have neither body nor expression body.
            if (node.Body == null && node.ExpressionBody == null)
                return node;

            return node.WithBody(CreateThrowBlock()).WithExpressionBody(null).WithSemicolonToken(default);
        }

        public override SyntaxNode VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            if (node.Body == null && node.ExpressionBody == null)
                return node;

            string key = ApiKey.GetOperatorKey((TypeDeclarationSyntax)node.Parent, node.OperatorToken.ValueText);
            SyntaxNode result = node.WithBody(CreateThrowBlock()).WithExpressionBody(null).WithSemicolonToken(default);
            return ApplyDocComment(result, key);
        }

        public override SyntaxNode VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            if (node.Body == null && node.ExpressionBody == null)
                return node;

            string key = ApiKey.GetConversionOperatorKey((TypeDeclarationSyntax)node.Parent, node.ImplicitOrExplicitKeyword.ValueText);
            SyntaxNode result = node.WithBody(CreateThrowBlock()).WithExpressionBody(null).WithSemicolonToken(default);
            return ApplyDocComment(result, key);
        }

        public override SyntaxNode VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            // Strip non-const field initializers so that implementation sources with runtime
            // initializers (e.g., `private static readonly X s_x = new X();`) compile correctly.
            if (node.Initializer != null &&
                node.Parent is VariableDeclarationSyntax &&
                node.Parent.Parent is FieldDeclarationSyntax fieldDecl &&
                !fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword))
            {
                return node.WithInitializer(null);
            }

            return base.VisitVariableDeclarator(node);
        }

        /// <summary>
        /// If implementation sources are available, replaces the node's XML doc comment trivia
        /// with the implementation's doc comment.  Records a warning-level diagnostic if the API
        /// could not be found or has no documentation.
        /// </summary>
        private SyntaxNode ApplyDocComment(SyntaxNode node, string key)
        {
            if (_docIndex == null || node == null)
                return node;

            if (_docIndex.TryGetDocComment(key, out SyntaxTriviaList implDocTrivia))
            {
                return node.WithLeadingTrivia(ReplaceDocTrivia(node.GetLeadingTrivia(), implDocTrivia));
            }

            if (!_docIndex.HasMember(key))
                _apisNotFoundInImplementation.Add(key);
            else
                _apisMissingDocs.Add(key);

            return node;
        }

        /// <summary>
        /// Replaces any XML doc comment trivia in <paramref name="existing"/> with
        /// <paramref name="implDocs"/>, preserving surrounding whitespace/indentation.
        /// </summary>
        private static SyntaxTriviaList ReplaceDocTrivia(SyntaxTriviaList existing, SyntaxTriviaList implDocs)
        {
            // Remove existing doc-comment trivia.
            var withoutDocs = existing
                .Where(t => !t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) &&
                            !t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                .ToList();

            // Insert implementation docs just before the trailing indentation whitespace that
            // immediately precedes the declaration keyword.
            int insertAt = withoutDocs.Count;
            while (insertAt > 0 && withoutDocs[insertAt - 1].IsKind(SyntaxKind.WhitespaceTrivia))
                insertAt--;

            withoutDocs.InsertRange(insertAt, implDocs);
            return SyntaxFactory.TriviaList(withoutDocs);
        }

        private string GetDefaultMessage() => "{ throw new System.PlatformNotSupportedException(" + $"{ _message }); " + " }\n";

        private BlockSyntax CreateThrowBlock() => (BlockSyntax)SyntaxFactory.ParseStatement(GetDefaultMessage());

        private static BlockSyntax CreateEmptyBlock() => (BlockSyntax)SyntaxFactory.ParseStatement("{ }\n");
    }
}
