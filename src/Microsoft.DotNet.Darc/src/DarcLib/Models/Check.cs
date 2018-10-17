// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    public class Check
    {
        public Check(CheckState status, string name, string url)
        {
            Status = status;
            Name = name;
            Url = url;
        }

        public CheckState Status { get; }
        public string Name { get; }
        public string Url { get; }
    }
}
