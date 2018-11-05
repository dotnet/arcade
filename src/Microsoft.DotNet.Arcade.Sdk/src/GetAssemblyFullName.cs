// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Arcade.Sdk
{
    public class GetAssemblyFullName : Task
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Required]
        public string PathMetadata { get; set; }

        [Required]
        public string FullNameMetadata { get; set; }

        [Output]
        public ITaskItem[] ItemsWithFullName { get; set; }

        public override bool Execute()
        {
            ItemsWithFullName = Items;

            foreach (var item in Items)
            {
                item.SetMetadata(FullNameMetadata, AssemblyName.GetAssemblyName(item.GetMetadata(PathMetadata)).FullName);
            }

            return true;
        }
    }
}

