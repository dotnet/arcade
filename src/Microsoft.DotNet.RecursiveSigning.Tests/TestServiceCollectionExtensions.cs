// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Arcade.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Configuration;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    /// <summary>
    /// Extension methods for configuring test-specific RecursiveSigning services.
    /// </summary>
    public static class TestServiceCollectionExtensions
    {
        /// <summary>
        /// Add stub implementations for Phase 1 testing.
        /// </summary>
        public static IServiceCollection AddStubImplementations(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IFileAnalyzer, StubFileAnalyzer>();
            services.AddSingleton<ISignatureCalculator, StubSignatureCalculator>();

            return services;
        }

        /// <summary>
        /// Add fake signing provider for testing.
        /// </summary>
        public static IServiceCollection AddFakeSigningProvider(
            this IServiceCollection services,
            TimeSpan? delay = null,
            bool shouldFail = false)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<ISigningProvider>(sp =>
            {
                var fileSystem = sp.GetService<IFileSystem>();
                return new FakeSigningProvider(fileSystem, delay, shouldFail);
            });

            return services;
        }

        /// <summary>
        /// Add stub container handler for testing.
        /// </summary>
        public static IServiceCollection AddStubContainerHandler(this IServiceCollection services)
        {
            return services.AddContainerHandler<StubContainerHandler>();
        }
    }
}
