﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace XliffTasks.Tasks
{
    internal sealed class BuildErrorException : Exception
    {
        public BuildErrorException(string message) : base(message)
        {
        }
    }
}
