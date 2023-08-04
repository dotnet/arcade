// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.VersionTools.Cli;

public interface IOperation
{
    enum ExitCodes : int
    {
        ERROR_SUCCESS = 0, // (0x0) The operation completed successfully.
        ERROR_FILE_NOT_FOUND = 2, // (0x2) The system cannot find the file specified.
    }

    ExitCodes Execute();
}
