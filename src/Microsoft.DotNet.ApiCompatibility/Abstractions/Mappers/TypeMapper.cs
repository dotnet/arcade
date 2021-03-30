﻿using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class TypeMapper : ElementMapper<ITypeSymbol>
    {
        private Dictionary<string, TypeMapper> _nestedTypes;
        private Dictionary<string, MemberMapper> _members;

        public TypeMapper(DiffingSettings settings) : base(settings) { }

        public bool ShouldDiffMembers => Left != null && Right != null;

        public IEnumerable<TypeMapper> GetNestedTypes()
        {
            if (_nestedTypes == null)
            {
                _nestedTypes = new Dictionary<string, TypeMapper>();

                if (Left != null)
                {
                    AddOrCreateMappers(Left.GetTypeMembers(), 0);
                }

                if (Right != null)
                {
                    AddOrCreateMappers(Right.GetTypeMembers(), 1);
                }

                void AddOrCreateMappers(IEnumerable<ITypeSymbol> symbols, int index)
                {
                    foreach (var nestedType in symbols)
                    {
                        if (Settings.Filter.Include(nestedType))
                        {
                            if (!_nestedTypes.TryGetValue(nestedType.Name, out TypeMapper mapper))
                            {
                                mapper = new TypeMapper(Settings);
                                _nestedTypes.Add(nestedType.Name, mapper);
                            }
                            mapper.AddElement(nestedType, index);
                        }
                    }
                }
            }

            return _nestedTypes.Values;
        }

        public IEnumerable<MemberMapper> GetMembers()
        {
            if (_members == null)
            {
                _members = new Dictionary<string, MemberMapper>();

                if (Left != null)
                {
                    AddOrCreateMappers(Left.GetMembers(), 0);
                }

                if (Right != null)
                {
                    AddOrCreateMappers(Right.GetMembers(), 1);
                }

                void AddOrCreateMappers(IEnumerable<ISymbol> symbols, int index)
                {
                    foreach (var member in symbols)
                    {
                        if (Settings.Filter.Include(member) && member is not ITypeSymbol)
                        {
                            string displayString = member.ToDisplayString();
                            if (!_members.TryGetValue(displayString, out MemberMapper mapper))
                            {
                                mapper = new MemberMapper(Settings);
                                _members.Add(displayString, mapper);
                            }
                            mapper.AddElement(member, index);
                        }
                    }
                }
            }

            return _members.Values;
        }
    }
}