// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.SignCheck.Logging
{
    public interface ILogger
    {
        bool HasLoggedErrors
        {
            get;
        }

        void Close();

        void WriteMessage(string message);

        void WriteMessage(LogVerbosity verbosity, string message);

        void WriteMessage(LogVerbosity verbosity, string message, params object[] values);

        void WriteLine();

        void WriteError(string message);

        void WriteError(LogVerbosity verbosity, string message);

        void WriteError(LogVerbosity verbosity, string message, params object[] values);
    }
}
