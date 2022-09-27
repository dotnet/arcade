// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

public class AssemblySymbolDisplayFormats
{
    public static readonly SymbolDisplayFormat NamespaceDisplayFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    public static readonly SymbolDisplayFormat NamedTypeDisplayFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
        genericsOptions: 
            SymbolDisplayGenericsOptions.IncludeTypeParameters |
            SymbolDisplayGenericsOptions.IncludeVariance);

    public static readonly SymbolDisplayFormat BaseTypeDisplayFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions:
            SymbolDisplayGenericsOptions.IncludeTypeConstraints |
            SymbolDisplayGenericsOptions.IncludeTypeParameters |
            SymbolDisplayGenericsOptions.IncludeVariance);

    public static readonly SymbolDisplayFormat MemberDisplayFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
        propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
        kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
        parameterOptions: SymbolDisplayParameterOptions.IncludeDefaultValue |
            SymbolDisplayParameterOptions.IncludeExtensionThis |
            SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeParamsRefOut |
            SymbolDisplayParameterOptions.IncludeType,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeConstraints |
            SymbolDisplayGenericsOptions.IncludeTypeParameters |
            SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeExplicitInterface |
            SymbolDisplayMemberOptions.IncludeConstantValue |
            SymbolDisplayMemberOptions.IncludeModifiers |
            SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType |
            SymbolDisplayMemberOptions.IncludeAccessibility);
}
