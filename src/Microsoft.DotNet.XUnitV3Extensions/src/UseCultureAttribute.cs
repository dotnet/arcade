// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Ported from https://github.com/xunit/samples.xunit/blob/main/v3/UseCultureExample/UseCultureAttribute.cs

#nullable enable

using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Xunit.v3;

namespace Microsoft.DotNet.XUnitExtensions
{
    /// <summary>
    /// Apply this attribute to your test method to replace the
    /// <see cref="Thread.CurrentThread" /> <see cref="CultureInfo.CurrentCulture" /> and
    /// <see cref="CultureInfo.CurrentUICulture" /> with another culture.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class UseCultureAttribute : BeforeAfterTestAttribute
    {
        private readonly Lazy<CultureInfo> _culture;
        private readonly Lazy<CultureInfo> _uiCulture;

        private CultureInfo? _originalCulture;
        private CultureInfo? _originalUICulture;

        /// <summary>
        /// Replaces the culture and UI culture of the current thread with
        /// <paramref name="culture" />.
        /// </summary>
        /// <param name="culture">The name of the culture.</param>
        /// <remarks>
        /// This constructor overload uses <paramref name="culture" /> for both
        /// <see cref="Culture" /> and <see cref="UICulture" />.
        /// </remarks>
        public UseCultureAttribute(string culture)
            : this(culture, culture) { }

        /// <summary>
        /// Replaces the culture and UI culture of the current thread with
        /// <paramref name="culture" /> and <paramref name="uiCulture" />.
        /// </summary>
        /// <param name="culture">The name of the culture.</param>
        /// <param name="uiCulture">The name of the UI culture.</param>
        public UseCultureAttribute(string culture, string uiCulture)
        {
            _culture = new Lazy<CultureInfo>(() => new CultureInfo(culture, false));
            _uiCulture = new Lazy<CultureInfo>(() => new CultureInfo(uiCulture, false));
        }

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public CultureInfo Culture => _culture.Value;

        /// <summary>
        /// Gets the UI culture.
        /// </summary>
        public CultureInfo UICulture => _uiCulture.Value;

        /// <summary>
        /// Stores the current <see cref="CultureInfo.CurrentCulture" /> and
        /// <see cref="CultureInfo.CurrentUICulture" /> and replaces them with the
        /// new cultures defined in the constructor.
        /// </summary>
        /// <param name="methodUnderTest">The method under test.</param>
        /// <param name="test">The test that is about to be run.</param>
        public override void Before(MethodInfo methodUnderTest, IXunitTest test)
        {
            _originalCulture = Thread.CurrentThread.CurrentCulture;
            _originalUICulture = Thread.CurrentThread.CurrentUICulture;

            Thread.CurrentThread.CurrentCulture = Culture;
            Thread.CurrentThread.CurrentUICulture = UICulture;
        }

        /// <summary>
        /// Restores the original <see cref="CultureInfo.CurrentCulture" /> and
        /// <see cref="CultureInfo.CurrentUICulture" /> on the current thread.
        /// </summary>
        /// <param name="methodUnderTest">The method under test.</param>
        /// <param name="test">The test that just finished running.</param>
        public override void After(MethodInfo methodUnderTest, IXunitTest test)
        {
            if (_originalCulture is not null)
                Thread.CurrentThread.CurrentCulture = _originalCulture;
            if (_originalUICulture is not null)
                Thread.CurrentThread.CurrentUICulture = _originalUICulture;
        }
    }
}
