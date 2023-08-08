// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.DeltaBuild;

public record ProjectDependencies(
    Dictionary<FilePath, HashSet<FilePath>> Projects,
    Dictionary<FilePath, HashSet<FilePath>> Files);
