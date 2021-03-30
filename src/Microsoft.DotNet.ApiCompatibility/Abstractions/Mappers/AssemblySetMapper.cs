using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class AssemblySetMapper : ElementMapper<IEnumerable<IAssemblySymbol>>
    {
        private Dictionary<string, AssemblyMapper> _assemblies;

        public AssemblySetMapper(DiffingSettings settings) : base(settings) { }

        public IEnumerable<AssemblyMapper> GetAssemblies()
        {
            if (_assemblies == null)
            {
                _assemblies = new Dictionary<string, AssemblyMapper>();

                if (Left != null)
                {
                    AddOrCreateMappers(Left, 0);
                }

                if (Right != null)
                {
                    AddOrCreateMappers(Right, 1);
                }

                void AddOrCreateMappers(IEnumerable<IAssemblySymbol> elements, int index)
                {
                    foreach (IAssemblySymbol assembly in elements)
                    {
                        if (!_assemblies.TryGetValue(assembly.Name, out AssemblyMapper mapper))
                        {
                            mapper = new AssemblyMapper(Settings);
                            _assemblies.Add(assembly.Name, mapper);
                        }
                        mapper.AddElement(assembly, index);
                    }
                }
            }

            return _assemblies.Values;
        }
    }
}
