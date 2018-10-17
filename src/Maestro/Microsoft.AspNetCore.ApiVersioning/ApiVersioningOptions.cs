// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.ApiVersioning.Schemes;

namespace Microsoft.AspNetCore.ApiVersioning
{
    [PublicAPI]
    public class ApiVersioningOptions
    {
        public Func<TypeInfo, string> GetVersion { get; set; } = DefaultGetVersion;
        public Func<TypeInfo, string> GetName { get; set; } = DefaultGetName;
        public IVersioningScheme VersioningScheme { get; set; } = new PathVersioningScheme();

        public static string DefaultGetVersion(TypeInfo controllerType)
        {
            var attribute = controllerType.GetCustomAttribute<ApiVersionAttribute>(false);
            return attribute?.Version;
        }

        private static string DefaultGetName(TypeInfo controllerType)
        {
            return controllerType.Name.Substring(0, controllerType.Name.Length - 10);
        }
    }

    [PublicAPI]
    public class ApiVersionAttribute : Attribute
    {
        public ApiVersionAttribute(string version)
        {
            Version = version;
        }

        public string Version { get; }
    }
}
