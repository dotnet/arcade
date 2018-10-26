// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.ApiVersioning.Analyzers
{
    internal class KnownTypes
    {
        public KnownTypes(Compilation compilation)
        {
            Controller = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Controller");
            Type = compilation.GetTypeByMetadataName("System.Type");
            ApiVersionAttribute =
                compilation.GetTypeByMetadataName("Microsoft.AspNetCore.ApiVersioning.ApiVersionAttribute");
            ProducesResponseTypeAttribute =
                compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute");
        }

        public ITypeSymbol Type { get; }
        public ITypeSymbol Controller { get; }
        public ITypeSymbol ApiVersionAttribute { get; }
        public ITypeSymbol ProducesResponseTypeAttribute { get; }

        public bool HaveRequired()
        {
            return Type != null && Controller != null && ApiVersionAttribute != null &&
                   ProducesResponseTypeAttribute != null;
        }
    }
}
