// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if COREFX
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Diagnostics
{
    // Below taken from Reference Source
    // Outputs trace messages to the console.
    public class ConsoleTraceListener : TextWriterTraceListener
    {
        public ConsoleTraceListener()
            : base(Console.Out)
        { }

        public ConsoleTraceListener(bool useErrorStream)
            : base(useErrorStream ? Console.Error : Console.Out)
        { }
    }
}
#endif
