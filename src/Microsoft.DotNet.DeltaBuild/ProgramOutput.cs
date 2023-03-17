// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System.Collections.Generic;

namespace Microsoft.DotNet.DeltaBuild;

internal record ProgramOutput(IList<string> AffectedProjectChain, IList<string> AffectedProjects);
