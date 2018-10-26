// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    internal interface ILocal
    {
        Task UpdateDependenciesAsync(List<DependencyDetail> dependencies, IRemote remote);
        /// <summary>
        ///     Verify the local repository has correct and consistent dependency information
        /// </summary>
        /// <returns>Async task</returns>
        Task<bool> Verify();
    }
}
