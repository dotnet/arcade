// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Cci.Mappings;

#if COREFX
using System.Reflection;
using System.Composition.Hosting;
using CompositionContainer = System.Composition.Hosting.CompositionHost;
#else
using System.ComponentModel.Composition.Hosting;
#endif

namespace Microsoft.Cci.Differs
{
    public class ElementDifferenceFactory : IElementDifferenceFactory
    {
        private CompositionContainer _container;
        private IDifferenceRule[] _diffRules;
        private Func<IDifferenceRuleMetadata, bool> _ruleFilter;

        public ElementDifferenceFactory()
        {
        }

        public ElementDifferenceFactory(CompositionContainer container, Func<IDifferenceRuleMetadata, bool> ruleFilter = null)
        {
            Contract.Requires(container != null);
            _container = container;
            _ruleFilter = ruleFilter;
        }

        public IDifferences GetDiffer<T>(ElementMapping<T> element) where T : class
        {
            return new ElementDiffer<T>(element, GetDifferenceRules<T>());
        }

        private IDifferenceRule[] GetDifferenceRules<T>() where T : class
        {
            EnsureContainer();

            if (_diffRules == null)
            {
#if COREFX
                IEnumerable<IDifferenceRule> rules = _container.GetExports<IDifferenceRule>();
                _diffRules = rules.ToArray();
#else
                IEnumerable<Lazy<IDifferenceRule, IDifferenceRuleMetadata>> lazyRules = _container.GetExports<IDifferenceRule, IDifferenceRuleMetadata>();
                if (_ruleFilter != null)
                {
                    lazyRules = lazyRules.Where(l => _ruleFilter(l.Metadata));
                }
                _diffRules = lazyRules.Select(l => l.Value).ToArray();
#endif
            }

            return _diffRules;
        }

        private void EnsureContainer()
        {
            if (_container != null)
                return;
#if COREFX
            var configuration = new ContainerConfiguration().WithAssembly(typeof(ElementDifferenceFactory).GetTypeInfo().Assembly);
            _container = configuration.CreateContainer();
#else
            _container = new CompositionContainer(new AssemblyCatalog(typeof(ElementDifferenceFactory).Assembly));
#endif
        }
    }
}
