// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Internal.Microsoft.Extensions.DependencyModel
{
    internal interface IDependencyContextReader: IDisposable
    {
        DependencyContext Read(Stream stream);
    }
}
