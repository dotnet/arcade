// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Composition.Hosting.Core;
using System.Linq;

namespace Microsoft.Fx.ApiReviews.Differencing.Helpers
{
    static class MefHelpers
    {
        public static ContainerConfiguration WithExport<T>(this ContainerConfiguration configuration, T exportedInstance, string contractName = null, IDictionary<string, object> metadata = null)
        {
            return WithExport(configuration, exportedInstance, typeof(T), contractName, metadata);
        }

        public static ContainerConfiguration WithExport(this ContainerConfiguration configuration, object exportedInstance, Type contractType, string contractName = null, IDictionary<string, object> metadata = null)
        {
            return configuration.WithProvider(new InstanceExportDescriptorProvider(
                exportedInstance, contractType, contractName, metadata));
        }

        public static ContainerConfiguration WithFactoryDelegate<T>(this ContainerConfiguration configuration, Func<T> exportedInstanceFactory, string contractName = null, IDictionary<string, object> metadata = null, bool isShared = false)
        {
            return WithFactoryDelegate(configuration, () => exportedInstanceFactory(), typeof(T), contractName, metadata, isShared);
        }

        public static ContainerConfiguration WithFactoryDelegate(this ContainerConfiguration configuration, Func<object> exportedInstanceFactory, Type contractType, string contractName = null, IDictionary<string, object> metadata = null, bool isShared = false)
        {
            return configuration.WithProvider(new DelegateExportDescriptorProvider(
                exportedInstanceFactory, contractType, contractName, metadata, isShared));
        }

        abstract class SinglePartExportDescriptorProvider : ExportDescriptorProvider
        {
            readonly Type _contractType;
            readonly string _contractName;
            readonly IDictionary<string, object> _metadata;

            protected SinglePartExportDescriptorProvider(Type contractType, string contractName, IDictionary<string, object> metadata)
            {
                if (contractType == null) throw new ArgumentNullException("contractType");

                _contractType = contractType;
                _contractName = contractName;
                _metadata = metadata ?? new Dictionary<string, object>();
            }

            protected bool IsSupportedContract(CompositionContract contract)
            {
                if (contract.ContractType != _contractType ||
                    contract.ContractName != _contractName)
                    return false;

                if (contract.MetadataConstraints != null)
                {
                    var subsetOfConstraints = contract.MetadataConstraints.Where(c => _metadata.ContainsKey(c.Key)).ToDictionary(c => c.Key, c => _metadata[c.Key]);
                    var constrainedSubset = new CompositionContract(contract.ContractType, contract.ContractName,
                        subsetOfConstraints.Count == 0 ? null : subsetOfConstraints);

                    if (!contract.Equals(constrainedSubset))
                        return false;
                }

                return true;
            }

            protected IDictionary<string, object> Metadata { get { return _metadata; } }
        }

        // This one-instance-per-provider design is not efficient for more than a few instances;
        // we're just aiming to show the mechanics here.
        class InstanceExportDescriptorProvider : SinglePartExportDescriptorProvider
        {
            object _exportedInstance;

            public InstanceExportDescriptorProvider(object exportedInstance, Type contractType, string contractName, IDictionary<string, object> metadata)
                : base(contractType, contractName, metadata)
            {
                if (exportedInstance == null) throw new ArgumentNullException("exportedInstance");
                _exportedInstance = exportedInstance;
            }

            public override IEnumerable<ExportDescriptorPromise> GetExportDescriptors(CompositionContract contract, DependencyAccessor descriptorAccessor)
            {
                if (IsSupportedContract(contract))
                    yield return new ExportDescriptorPromise(contract, _exportedInstance.ToString(), true, NoDependencies, _ =>
                        ExportDescriptor.Create((c, o) => _exportedInstance, Metadata));
            }
        }

        class DelegateExportDescriptorProvider : SinglePartExportDescriptorProvider
        {
            CompositeActivator _activator;

            public DelegateExportDescriptorProvider(Func<object> exportedInstanceFactory, Type contractType, string contractName, IDictionary<string, object> metadata, bool isShared)
                : base(contractType, contractName, metadata)
            {
                if (exportedInstanceFactory == null) throw new ArgumentNullException("exportedInstanceFactory");

                // Runs the factory method, validates the result and registers it for disposal if necessary.
                CompositeActivator constructor = (c, o) => {
                    var result = exportedInstanceFactory();
                    if (result == null)
                        throw new InvalidOperationException("Delegate factory returned null.");

                    if (result is IDisposable)
                        c.AddBoundInstance((IDisposable)result);

                    return result;
                };

                if (isShared)
                {
                    var sharingId = LifetimeContext.AllocateSharingId();
                    _activator = (c, o) => {
                        // Find the root composition scope.
                        var scope = c.FindContextWithin(null);
                        if (scope == c)
                        {
                            // We're already in the root scope, create the instance
                            return scope.GetOrCreate(sharingId, o, constructor);
                        }
                        else
                        {
                            // Composition is moving up the hierarchy of scopes; run
                            // a new operation in the root scope.
                            return CompositionOperation.Run(scope, (c1, o1) => c1.GetOrCreate(sharingId, o1, constructor));
                        }
                    };
                }
                else
                {
                    _activator = constructor;
                }
            }

            public override IEnumerable<ExportDescriptorPromise> GetExportDescriptors(CompositionContract contract, DependencyAccessor descriptorAccessor)
            {
                if (IsSupportedContract(contract))
                    yield return new ExportDescriptorPromise(contract, "factory delegate", true, NoDependencies, _ => ExportDescriptor.Create(_activator, Metadata));
            }
        }
    }
}
