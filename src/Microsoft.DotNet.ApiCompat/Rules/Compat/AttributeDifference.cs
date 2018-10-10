// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Cci.Mappings;
using System.Composition;
using Microsoft.Cci.Comparers;

namespace Microsoft.Cci.Differs.Rules
{
    internal class AssemblyAttributeDifferences : AttributeDifference
    {
        public override DifferenceType Diff(IDifferences differences, IAssembly impl, IAssembly contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            bool added = false;

            //added |= AnyAttributeAdded(differences, impl, impl.AssemblyAttributes, contract.AssemblyAttributes);
            //added |= AnyAttributeAdded(differences, impl, impl.ModuleAttributes, contract.ModuleAttributes);
            //added |= AnySecurityAttributeAdded(differences, impl, impl.SecurityAttributes, contract.SecurityAttributes);

            if (added)
                return DifferenceType.Changed;

            return DifferenceType.Unknown;
        }
    }

    [ExportDifferenceRule]
    internal class AttributeDifference : CompatDifferenceRule
    {
        private MappingSettings _settings = new MappingSettings();

        [Import]
        public IAttributeFilter AttributeFilter { get; set; }

        public override DifferenceType Diff(IDifferences differences, IAssembly impl, IAssembly contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            bool added = false;

            //added |= AnyAttributeAdded(differences, impl, impl.AssemblyAttributes, contract.AssemblyAttributes);
            //added |= AnyAttributeAdded(differences, impl, impl.ModuleAttributes, contract.ModuleAttributes);
            //added |= AnySecurityAttributeAdded(differences, impl, impl.SecurityAttributes, contract.SecurityAttributes);

            if (added)
                return DifferenceType.Changed;

            return DifferenceType.Unknown;
        }

        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            return CheckAttributeDifferences(differences, impl, impl.Attributes, contract.Attributes);
        }

        public override DifferenceType Diff(IDifferences differences, ITypeDefinitionMember impl, ITypeDefinitionMember contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            bool added = false;

            //added |= AnyAttributeAdded(differences, impl, impl.Attributes, contract.Attributes);
            //added |= AnyMethodSpecificAttributeAdded(differences, impl as IMethodDefinition, contract as IMethodDefinition);

            if (added)
                return DifferenceType.Changed;

            return DifferenceType.Unknown;
        }

        private bool AnyMethodSpecificAttributeAdded(IDifferences differences, IMethodDefinition implMethod, IMethodDefinition contractMethod)
        {
            if (implMethod == null || contractMethod == null)
                return false;

            bool added = false;

            //added |= AnyAttributeAdded(differences, implMethod, implMethod.ReturnValueAttributes, contractMethod.ReturnValueAttributes);
            //added |= AnySecurityAttributeAdded(differences, implMethod, implMethod.SecurityAttributes, contractMethod.SecurityAttributes);

            //Debug.Assert(implMethod.ParameterCount == contractMethod.ParameterCount);

            //IParameterDefinition[] method1Params = implMethod.Parameters.ToArray();
            //IParameterDefinition[] method2Params = contractMethod.Parameters.ToArray();
            //for (int i = 0; i < implMethod.ParameterCount; i++)
            //    added |= AnyAttributeAdded(differences, method1Params[i], method1Params[i].Attributes, method2Params[i].Attributes);

            return added;
        }

        //private bool AnySecurityAttributeAdded(IDifferences differences, IReference target, IEnumerable<ISecurityAttribute> attribues1, IEnumerable<ISecurityAttribute> attributes2)
        //{
        //    return AnyAttributeAdded(differences, target, attribues1.SelectMany(a => a.Attributes), attributes2.SelectMany(a => a.Attributes));
        //}

        private DifferenceType CheckAttributeDifferences(IDifferences differences, IReference target, IEnumerable<ICustomAttribute> implAttributes, IEnumerable<ICustomAttribute> contractAttributes)
        {
            DifferenceType difference = DifferenceType.Unchanged;
            AttributesMapping<IEnumerable<ICustomAttribute>> attributeMapping = new AttributesMapping<IEnumerable<ICustomAttribute>>(_settings);
            AttributeComparer attributeComparer = new AttributeComparer();
            attributeMapping.AddMapping(0, contractAttributes.OrderBy(a => a, attributeComparer));
            attributeMapping.AddMapping(1, implAttributes.OrderBy(a => a, attributeComparer));

            foreach (var group in attributeMapping.Attributes)
            {
                switch (group.Difference)
                {
                    case DifferenceType.Added:
                        {
                            ITypeReference type = group.Representative.Attributes.First().Type;

                            if (AttributeFilter.ShouldExclude(type.DocId()))
                                break;

                            // Allow for additions
                            differences.Add(new Difference("AddedAttribute",
                                $"Attribute '{type.FullName()}' exists on '{target.FullName()}' in the {Implementation} but not the {Contract}."));

                            if (difference < DifferenceType.Added)
                            {
                                difference = DifferenceType.Added;
                            }
                            break;
                        }
                    case DifferenceType.Changed:
                        {
                            ITypeReference type = group.Representative.Attributes.First().Type;

                            if (AttributeFilter.ShouldExclude(type.DocId()))
                                break;

                            string contractKey = attributeComparer.GetKey(group[0].Attributes.First());
                            string implementationKey = attributeComparer.GetKey(group[1].Attributes.First());

                            differences.AddIncompatibleDifference("CannotChangeAttribute",
                                $"Attribute '{type.FullName()}' on '{target.FullName()}' changed from '{contractKey}' in the {Contract} to '{implementationKey}' in the {Implementation}.");

                            if (difference < DifferenceType.Changed)
                            {
                                difference = DifferenceType.Changed;
                            }
                            break;
                        }

                    case DifferenceType.Removed:
                        {
                            ITypeReference type = group.Representative.Attributes.First().Type;

                            if (AttributeFilter.ShouldExclude(type.DocId()))
                                break;

                            differences.AddIncompatibleDifference("CannotRemoveAttribute",
                                $"Attribute '{type.FullName()}' exists on '{target.FullName()}' in the {Contract} but not the {Implementation}.");

                            difference = DifferenceType.Removed;
                            break;
                        }
                }
            }
            return difference;
        }
    }

    public interface IAttributeFilter
    {
        bool ShouldExclude(string attributeDocID);
    }

    public class AttributeFilter : IAttributeFilter
    {
        private HashSet<string> ignorableAttributes;

        public AttributeFilter(string docIdList)
        {
            ignorableAttributes = DocIdExtensions.ReadDocIds(docIdList);
        }

        public bool ShouldExclude(string attributeDocID)
        {
            return ignorableAttributes.Contains(attributeDocID);
        }
    }
}
