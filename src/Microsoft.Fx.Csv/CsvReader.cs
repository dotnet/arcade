// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Fx.Csv
{
    public abstract class CsvReader : IDisposable
    {
        protected CsvReader(CsvSettings settings)
        {
            Settings = settings;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public abstract IEnumerable<string> Read();

        public CsvSettings Settings { get; set; }
    }
}
