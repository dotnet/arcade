// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.Build.Tasks.VisualStudio
{
    internal sealed class ProductOptProfTraining
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "tests")]
        public OptProfTrainingTest[] Tests { get; set; }
    }
}
