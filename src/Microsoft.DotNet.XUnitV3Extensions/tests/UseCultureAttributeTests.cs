// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Threading;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace Microsoft.DotNet.XUnitExtensions.Tests
{
    public class UseCultureAttributeTests
    {
        [Fact]
        public void SingleArgumentConstructorUsesSameCultureForBoth()
        {
            UseCultureAttribute attr = new("fr-FR");

            Assert.Equal("fr-FR", attr.Culture.Name);
            Assert.Equal("fr-FR", attr.UICulture.Name);
        }

        [Fact]
        public void TwoArgumentConstructorUsesDistinctCultures()
        {
            UseCultureAttribute attr = new("fr-FR", "de-DE");

            Assert.Equal("fr-FR", attr.Culture.Name);
            Assert.Equal("de-DE", attr.UICulture.Name);
        }

        [Fact]
        [UseCulture("fr-FR")]
        public void AttributeReplacesCurrentCulture()
        {
            Assert.Equal("fr-FR", Thread.CurrentThread.CurrentCulture.Name);
            Assert.Equal("fr-FR", Thread.CurrentThread.CurrentUICulture.Name);
        }

        [Fact]
        [UseCulture("fr-FR", "de-DE")]
        public void AttributeReplacesCurrentAndUICulture()
        {
            Assert.Equal("fr-FR", Thread.CurrentThread.CurrentCulture.Name);
            Assert.Equal("de-DE", Thread.CurrentThread.CurrentUICulture.Name);
        }

        [Fact]
        public void AttributeRestoresOriginalCultureAfterTest()
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo originalUICulture = Thread.CurrentThread.CurrentUICulture;

            UseCultureAttribute attr = new("fr-FR", "de-DE");

            attr.Before(methodUnderTest: null!, test: null!);

            Assert.Equal("fr-FR", Thread.CurrentThread.CurrentCulture.Name);
            Assert.Equal("de-DE", Thread.CurrentThread.CurrentUICulture.Name);

            attr.After(methodUnderTest: null!, test: null!);

            Assert.Equal(originalCulture, Thread.CurrentThread.CurrentCulture);
            Assert.Equal(originalUICulture, Thread.CurrentThread.CurrentUICulture);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("fr-FR")]
        [InlineData("de-DE")]
        [InlineData("ja-JP")]
        public void CultureIsLazilyConstructed(string cultureName)
        {
            UseCultureAttribute attr = new(cultureName);

            // Accessing the property forces the lazy CultureInfo to be created.
            Assert.Equal(cultureName, attr.Culture.Name);
            Assert.Equal(cultureName, attr.UICulture.Name);
        }
    }
}
