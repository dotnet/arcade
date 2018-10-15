// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.SignCheck.Logging
{
    public abstract class LoggerBase
    {
        protected LogVerbosity Verbosity
        {
            get;
            set;
        }

        public bool HasLoggedErrors
        {
            get;
            protected set;
        }

        public LoggerBase(LogVerbosity verbosity)
        {
            Verbosity = verbosity;
        }

        public virtual void Close()
        {

        }
    }
}
