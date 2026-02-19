// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Arcade.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Implementation;

namespace Microsoft.DotNet.RecursiveSigning.Configuration
{
    /// <summary>
    /// Extension methods for configuring RecursiveSigning services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add RecursiveSigning core services.
        /// Consumers must also register:
        /// - IFileAnalyzer
        /// - ISignatureCalculator  
        /// - ISigningProvider
        /// - IContainerHandler implementations (optional)
        /// </summary>
        public static IServiceCollection AddRecursiveSigning(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // Core orchestration services
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<IRecursiveSigning, Implementation.RecursiveSigning>();
            services.AddSingleton<ISigningGraph, SigningGraph>();
            services.AddSingleton<IFileDeduplicator, DefaultFileDeduplicator>();
            services.AddSingleton<IContainerHandlerRegistry>(sp =>
            {
                var registry = new ContainerHandlerRegistry();
                
                // Register all IContainerHandler implementations with the registry
                var handlers = sp.GetServices<IContainerHandler>();
                foreach (var handler in handlers)
                {
                    registry.RegisterHandler(handler);
                }
                
                return registry;
            });

            return services;
        }

        /// <summary>
        /// Add a container handler to the registry.
        /// </summary>
        public static IServiceCollection AddContainerHandler<THandler>(this IServiceCollection services)
            where THandler : class, IContainerHandler
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // Register the handler as IContainerHandler
            // The registry will pick it up when it's created
            services.AddSingleton<IContainerHandler, THandler>();

            return services;
        }
    }
}
