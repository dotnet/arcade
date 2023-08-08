// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.DeltaBuild;

public record ProjectProperties(
    string RepositoryPath,
    string ProjectFullPath,
    string ProjectDirectory);
