// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;

namespace Maestro.Contracts
{
    [DataContract]
    public class Asset
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Version { get; set; }
    }
}
