// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using System;

namespace Micorosft.DotNet.GenFacdes.ILRewriter
{
    internal sealed class TypeReferenceRewriter : MetadataRewriter
    {
        private readonly Func<ITypeReference, ITypeReference> transform;

        public TypeReferenceRewriter(IMetadataHost host, Func<ITypeReference, ITypeReference> transform): base(host, true)
        {
            this.transform = transform;
        }

        T TransformType<T>(T oldValue) where T: class, ITypeReference
        {
            return transform(oldValue) as T;
        }

        public override INamespaceTypeReference Rewrite(INamespaceTypeReference namespaceTypeReference)
        {
            return base.Rewrite(TransformType(namespaceTypeReference));
        }

        public override INestedTypeReference Rewrite(INestedTypeReference nestedTypeReference)
        {
            return base.Rewrite(TransformType(nestedTypeReference));
        }

        public override void RewriteChildren(GenericTypeInstanceReference genericTypeInstanceReference)
        {
            genericTypeInstanceReference.GenericType = TransformType(genericTypeInstanceReference.GenericType);
            base.RewriteChildren(genericTypeInstanceReference);
            genericTypeInstanceReference.GenericArguments = this.Rewrite(genericTypeInstanceReference.GenericArguments);
            genericTypeInstanceReference.GenericType = this.Rewrite(genericTypeInstanceReference.GenericType);
        }
    }
}
