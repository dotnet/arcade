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
    private readonly Mock<IAssemblySymbolOrderProvider> _orderProvider = new(MockBehavior.Strict);
    private readonly Mock<IAssemblySymbolFilter> _assemblySymbolFilter = new(MockBehavior.Strict);
    private readonly Mock<ISyntaxWriter> _syntaxWriter = new(MockBehavior.Strict);

    public CSharpBuilderTests()
    {
        _orderProvider.Setup(o => o.Order(It.IsAny<IEnumerable<INamespaceSymbol>>())).Returns(
            (IEnumerable<INamespaceSymbol> v) => { return v; });
        _orderProvider.Setup(o => o.Order(It.IsAny<IEnumerable<INamedTypeSymbol>>())).Returns(
            (IEnumerable<INamedTypeSymbol> v) => { return v; });
        _orderProvider.Setup(o => o.Order(It.IsAny<IEnumerable<ISymbol>>())).Returns(
            (IEnumerable<ISymbol> v) => { return v; });

        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<INamespaceSymbol>())).Returns(true);
        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<INamespaceSymbol>())).Returns(true);
        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ITypeSymbol>())).Returns(true);
        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(true);

        var block = new Mock<IDisposable>();
        _syntaxWriter.Setup(o => o.WriteNamespace(It.IsAny<IEnumerable<string>>())).Returns(block.Object);
        _syntaxWriter.Setup(o => o.WriteTypeDefinition(
            It.IsAny<IEnumerable<SyntaxKind>>(),
            It.IsAny<IEnumerable<SyntaxKind>>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>())).Returns(block.Object);
        _syntaxWriter.Setup(o => o.WriteAttribute(It.IsAny<string>()));
        _syntaxWriter.Setup(o => o.WriteProperty(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()));
        _syntaxWriter.Setup(o => o.WriteEvent(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()));
        _syntaxWriter.Setup(o => o.WriteMethod(It.IsAny<string>(), It.IsAny<bool>()));
        _syntaxWriter.Setup(o => o.WriteField(It.IsAny<string>()));
    }

    [Fact]
    public void TestNamespaceGeneration()
    {
        var syntaxTree = """
            namespace A
            {
            namespace B {}

            namespace C.D {}
            }
            """;

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A", "B" }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A", "C" }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A", "C", "D" }));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public void TestStructAccessibilityGeneration()
    {
        var syntaxTree = """
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
            """;

        // filter out default constructors.
        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(false);

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "PublicStruct",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.InternalKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "InternalStruct",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.InternalKeyword },
            new[] { SyntaxKind.ReadOnlyKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "ReadonlyStruct",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.ReadOnlyKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "PublicReadonlyStruct",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.InternalKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "RecordStruct",
            new[] { "System.IEquatable<A.RecordStruct>" },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.InternalKeyword },
            new[] { SyntaxKind.ReadOnlyKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "ReadonlyRecordStruct",
            new[] { "System.IEquatable<A.ReadonlyRecordStruct>" },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.RefKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "PublicRefStruct",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.ReadOnlyKeyword, SyntaxKind.RefKeyword, SyntaxKind.PartialKeyword, SyntaxKind.StructKeyword },
            "PublicReadonlyRefStruct",
            new string[] { },
            new string[] { }));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public void TestClassAccessibilityGeneration()
    {
        var syntaxTree = """
            namespace A
            {
                public class PublicClass { }
                static class StaticInernalClass { }
                public sealed class PublicSealedClass { }
            }
            """;

        // filter out default constructors.
        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(false);

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "PublicClass",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.InternalKeyword },
            new[] { SyntaxKind.StaticKeyword, SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "StaticInernalClass",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.SealedKeyword, SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "PublicSealedClass",
            new string[] { },
            new string[] { }));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public void TestInterfaceGeneration()
    {
        var syntaxTree = """
            namespace A
            {
                public interface IPoint
                {
                    // Property signatures:
                    int X { get; set; }
                    int Y { get; set; }
                    
                    double CalculateDistance(IPoint p);
                }
            }
            """;

        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(FilterOutImplicitMethods);

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.InterfaceKeyword },
            "IPoint",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteProperty(
            "int X",
            /*hasImplementation*/false,
            /*hasGet*/true,
            /*hasSet*/true));

        _syntaxWriter.Verify(o => o.WriteProperty(
            "int Y",
            /*hasImplementation*/false,
            /*hasGet*/true,
            /*hasSet*/true));

        _syntaxWriter.Verify(o => o.WriteMethod(
            "double CalculateDistance(A.IPoint p)",
            /*hasImplementation*/false));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public void TestEnumGeneration()
    {
        var syntaxTree = """
            namespace A
            {
                public enum Color
                {
                    White = 0,
                    Green = 100,
                    Blue = 200,
                }
            }
            """;

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.EnumKeyword },
            "Color",
            new string[] { },
            new string[] { }));
        
        _syntaxWriter.Verify(o => o.WriteField("White = 0"));
        _syntaxWriter.Verify(o => o.WriteField("Green = 100"));
        _syntaxWriter.Verify(o => o.WriteField("Blue = 200"));
        _syntaxWriter.Verify(o => o.WriteField("White = 0"));

        _syntaxWriter.Verify(o => o.WriteMethod("public Color()", true));

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public void TestPropertyGeneration()
    {
        var syntaxTree = """
            namespace A
            {
                class Car
                {
                    int? Wheels { get; }
                    public bool IsRunning { get; set; }
                }
            }
            """;

        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(FilterOutImplicitMethods);

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.InternalKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "Car",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteProperty(
            "private int? Wheels",
            /*hasImplementation*/true,
            /*hasGet*/true,
            /*hasSet*/false));

        _syntaxWriter.Verify(o => o.WriteProperty(
            "public bool IsRunning",
            /*hasImplementation*/true,
            /*hasGet*/true,
            /*hasSet*/true));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public void TestAbstractPropertyGeneration()
    {
        var syntaxTree = """
            namespace A
            {
                abstract class Car
                {
                    abstract protected int? Wheels { get; }
                    abstract public bool IsRunning { get; set; }
                }
            }
            """;

        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(FilterOutImplicitMethods);

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.InternalKeyword },
            new[] { SyntaxKind.AbstractKeyword, SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "Car",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteProperty(
            "protected abstract int? Wheels",
            /*hasImplementation*/false,
            /*hasGet*/true,
            /*hasSet*/false));

        _syntaxWriter.Verify(o => o.WriteProperty(
            "public abstract bool IsRunning",
            /*hasImplementation*/false,
            /*hasGet*/true,
            /*hasSet*/true));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    [Fact]
    void TestExplicitInterfaceImplementation()
    {
        var syntaxTree = """
            namespace A
            {
                public interface IControl
                {
                    void Paint();
                }
                public interface ISurface
                {
                    void Paint();
                }

                public class SampleClass : IControl, ISurface
                {
                    void IControl.Paint()
                    {
                    }
                    void ISurface.Paint()
                    {
                    }
                }
            }
            """;

        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(FilterOutImplicitMethods);

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);


        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.InterfaceKeyword },
            "IControl",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.InterfaceKeyword },
            "ISurface",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "SampleClass",
            new[] { "A.IControl", "A.ISurface" },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteMethod("void Paint()", false));
        _syntaxWriter.Verify(o => o.WriteMethod("void Paint()", false));
        _syntaxWriter.Verify(o => o.WriteMethod("private void A.IControl.Paint()", true));
        _syntaxWriter.Verify(o => o.WriteMethod("private void A.ISurface.Paint()", true));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    [Fact]
    void TestPartiallySpecifiedGenericClassGeneration()
    {
        var syntaxTree = """
            namespace A
            {
                public class BaseNodeMultiple<T, U> { }

                public class Node4<T> : BaseNodeMultiple<T, int> { }

                public class Node5<T, U> : BaseNodeMultiple<T, U> { }
            }
            """;

        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(FilterOutImplicitMethods);

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword},
            "BaseNodeMultiple<T, U>",
            new string[] { },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "Node4<T>",
            new[] { "A.BaseNodeMultiple<T, int>" },
            new string[] { }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.PublicKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "Node5<T, U>",
            new[] { "A.BaseNodeMultiple<T, U>" },
            new string[] { }));

        _syntaxWriter.VerifyNoOtherCalls();
    }
    
    [Fact]
    void TestGenericClassWitConstraintsParameterGeneration()
    {
        var syntaxTree = """
            namespace A
            {
                class SuperKeyType<K, V, U>
                    where U : System.IComparable<U>
                    where V : new()
                { }
            }
            """;

        _assemblySymbolFilter.Setup(o => o.Include(It.IsAny<ISymbol>())).Returns(FilterOutImplicitMethods);

        var assembly = CompilationHelper.GetAssemblyFromSyntax(syntaxTree, enableNullable: false);

        var builder = new CSharpBuilder(
            _orderProvider.Object,
            _assemblySymbolFilter.Object,
            _syntaxWriter.Object);
        builder.WriteAssembly(assembly);

        _syntaxWriter.Verify(o => o.WriteNamespace(new string[] { /* global namespace */ }));
        _syntaxWriter.Verify(o => o.WriteNamespace(new[] { "A" }));

        _syntaxWriter.Verify(o => o.WriteTypeDefinition(
            new[] { SyntaxKind.InternalKeyword },
            new[] { SyntaxKind.PartialKeyword, SyntaxKind.ClassKeyword },
            "SuperKeyType<K, V, U>",
            new string[] { },
            new[] { "where V : new() ", "where U : System.IComparable<U>" }));

        _syntaxWriter.VerifyNoOtherCalls();
    }

    private bool FilterOutImplicitMethods(ISymbol member)
    {
        if (member.Kind == SymbolKind.NamedType || member.IsImplicitlyDeclared)
        {
            return false;
        }

        if (member is IMethodSymbol method)
        {
            if (method.MethodKind == MethodKind.PropertyGet ||
                method.MethodKind == MethodKind.PropertySet ||
                method.MethodKind == MethodKind.EventAdd ||
                method.MethodKind == MethodKind.EventRemove ||
                method.MethodKind == MethodKind.EventRaise)
            {
                return false;
            }
        }
        return true;
    }
}
