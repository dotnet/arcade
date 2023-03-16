// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System.Collections.Generic;

namespace DeltaBuild;

internal record ProgramOutput(IList<string> AffectedProjectChain, IList<string> AffectedProjects);
