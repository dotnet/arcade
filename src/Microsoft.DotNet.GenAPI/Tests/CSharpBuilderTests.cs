using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.GenAPI.Shared;
using Moq;
using Xunit;

namespace Microsoft.DotNet.GenAPI.Tests;

public class CSharpBuilderTests
{
    [Fact]
    public void BuildSimpleNamespacesTest()
    {
        var syntaxTree = @"
namespace A
{
namespace B {}

namespace C.D {}
}
";

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var orderProvider = new Mock<IAssemblySymbolOrderProvider>(MockBehavior.Strict);
        var assemblySymbolFilter = new Mock<IAssemblySymbolFilter>(MockBehavior.Strict);
        var syntaxWriter = new Mock<ISyntaxWriter>(MockBehavior.Strict);

        orderProvider.Setup(o => o.Order(It.IsAny<IEnumerable<INamespaceSymbol>>())).Returns(
            (IEnumerable<INamespaceSymbol> v) => { return v; });
        orderProvider.Setup(o => o.Order(It.IsAny<IEnumerable<INamedTypeSymbol>>())).Returns(
            (IEnumerable<INamedTypeSymbol> v) => { return v; });

        assemblySymbolFilter.Setup(o => o.Include(It.IsAny<INamespaceSymbol>())).Returns(true);

        var block = new Mock<IDisposable>();

        syntaxWriter.Setup(o => o.WriteNamespace(new string[] { /* global namespace */ })).Returns(block.Object);
        syntaxWriter.Setup(o => o.WriteNamespace(new string[] { "A" })).Returns(block.Object);
        syntaxWriter.Setup(o => o.WriteNamespace(new string[] { "A", "B" })).Returns(block.Object);
        syntaxWriter.Setup(o => o.WriteNamespace(new string[] { "A", "C" })).Returns(block.Object);
        syntaxWriter.Setup(o => o.WriteNamespace(new string[] { "A", "C", "D" })).Returns(block.Object);

        var builder = new CSharpBuilder(orderProvider.Object,
            assemblySymbolFilter.Object, syntaxWriter.Object);
        builder.WriteAssembly(assembly);
    }

    [Fact]
    public void BuildSimpleStructureTest()
    {
        var syntaxTree = @"
namespace A
{
    public struct PublicStruct { }
    struct InternalStruct { }
    readonly struct ReadonlyStruct { }
    public readonly struct PublicReadonlyStruct { }
    record struct RecordStruct { }
    readonly record struct ReadonlyRecordStruct { }
    public ref struct PublicRefStruct { }
    public readonly ref struct PublicReadonlyRefStruct { }
}
";

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var orderProvider = new Mock<IAssemblySymbolOrderProvider>(MockBehavior.Strict);
        var assemblySymbolFilter = new Mock<IAssemblySymbolFilter>(MockBehavior.Strict);
        var syntaxWriter = new Mock<ISyntaxWriter>(MockBehavior.Strict);

        orderProvider.Setup(o => o.Order(It.IsAny<IEnumerable<INamespaceSymbol>>())).Returns(
            (IEnumerable<INamespaceSymbol> v) => { return v; });
        orderProvider.Setup(o => o.Order(It.IsAny<IEnumerable<INamedTypeSymbol>>())).Returns(
            (IEnumerable<INamedTypeSymbol> v) => { return v; });
        orderProvider.Setup(o => o.Order(It.IsAny<IEnumerable<ISymbol>>())).Returns(
            new ISymbol[] { /* return empty list */ });

        assemblySymbolFilter.Setup(o => o.Include(It.IsAny<INamespaceSymbol>())).Returns(true);
        assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ITypeSymbol>())).Returns(true);
        assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(false);

        var block = new Mock<IDisposable>();

        syntaxWriter.Setup(o => o.WriteNamespace(new string[] { /* global namespace */ })).Returns(block.Object);
        syntaxWriter.Setup(o => o.WriteNamespace(new string[] { "A" })).Returns(block.Object);

        syntaxWriter.Setup(o => o.WriteTypeDefinition(
            new SyntaxKind[] { SyntaxKind.PublicKeyword },
            new SyntaxKind[] { SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "PublicStruct",
            new string[] { },
            new IEnumerable<SymbolDisplayPart>[] { })).Returns(block.Object);

        syntaxWriter.Setup(o => o.WriteTypeDefinition(
            new SyntaxKind[] { SyntaxKind.InternalKeyword },
            new SyntaxKind[] { SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "InternalStruct",
            new string[] { },
            new IEnumerable<SymbolDisplayPart>[] { })).Returns(block.Object);

        syntaxWriter.Setup(o => o.WriteTypeDefinition(
            new SyntaxKind[] { SyntaxKind.InternalKeyword },
            new SyntaxKind[] { SyntaxKind.ReadOnlyKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "ReadonlyStruct",
            new string[] { },
            new IEnumerable<SymbolDisplayPart>[] { })).Returns(block.Object);

        syntaxWriter.Setup(o => o.WriteTypeDefinition(
            new SyntaxKind[] { SyntaxKind.PublicKeyword },
            new SyntaxKind[] { SyntaxKind.ReadOnlyKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "PublicReadonlyStruct",
            new string[] { },
            new IEnumerable<SymbolDisplayPart>[] { })).Returns(block.Object);

        syntaxWriter.Setup(o => o.WriteTypeDefinition(
            new SyntaxKind[] { SyntaxKind.InternalKeyword },
            new SyntaxKind[] { SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "RecordStruct",
            new string[] { "System.IEquatable<A.RecordStruct>" },
            new IEnumerable<SymbolDisplayPart>[] { })).Returns(block.Object);

        syntaxWriter.Setup(o => o.WriteTypeDefinition(
            new SyntaxKind[] { SyntaxKind.InternalKeyword },
            new SyntaxKind[] { SyntaxKind.ReadOnlyKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "ReadonlyRecordStruct",
            new string[] { "System.IEquatable<A.ReadonlyRecordStruct>" },
            new IEnumerable<SymbolDisplayPart>[] { })).Returns(block.Object);

        syntaxWriter.Setup(o => o.WriteTypeDefinition(
            new SyntaxKind[] { SyntaxKind.PublicKeyword },
            new SyntaxKind[] { SyntaxKind.RefKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "PublicRefStruct",
            new string[] { },
            new IEnumerable<SymbolDisplayPart>[] { })).Returns(block.Object);

        syntaxWriter.Setup(o => o.WriteTypeDefinition(
            new SyntaxKind[] { SyntaxKind.PublicKeyword },
            new SyntaxKind[] { SyntaxKind.ReadOnlyKeyword, SyntaxKind.RefKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "PublicReadonlyRefStruct",
            new string[] { },
            new IEnumerable<SymbolDisplayPart>[] { })).Returns(block.Object);

        var builder = new CSharpBuilder(orderProvider.Object,
            assemblySymbolFilter.Object, syntaxWriter.Object);
        builder.WriteAssembly(assembly);
    }
}
