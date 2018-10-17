// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using JetBrains.Annotations;
using Microsoft.AspNetCore.ApiVersioning.Schemes;

namespace Microsoft.AspNetCore.ApiVersioning
{
    [PublicAPI]
    public static class ApiVersioningOptionsExtensions
    {
        public static ApiVersioningOptions VersionByHeader(this ApiVersioningOptions options)
        {
            return options.VersionByHeader("X-Api-Version");
        }

        public static ApiVersioningOptions VersionByHeader(this ApiVersioningOptions options, string headerName)
        {
            options.VersioningScheme = new HeaderVersioningScheme(headerName);
            return options;
        }

        public static ApiVersioningOptions VersionByQuery(this ApiVersioningOptions options)
        {
            return options.VersionByQuery("api-version");
        }

        public static ApiVersioningOptions VersionByQuery(this ApiVersioningOptions options, string parameterName)
        {
            options.VersioningScheme = new QueryVersioningScheme(parameterName);
            return options;
        }
    }
}
