// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Resources;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Arcade.Sdk
{
#if NET461
    [LoadInSeparateAppDomain]
    public class SetCorFlags : AppDomainIsolatedTask
    {
        static SetCorFlags() => AssemblyResolution.Initialize();
#else
    public class SetCorFlags : Task
    {
#endif
        [Required]
        public string FilePath { get; set; }

        public string AddFlags { get; set; }
        public string RemoveFlags { get; set; }

        private const int OffsetFromStartOfCorHeaderToFlags =
            4 + // byte count 
            2 + // Major version
            2 + // Minor version
            8;  // Metadata directory

        public override bool Execute()
        {
#if NET461
            AssemblyResolution.Log = Log;
#endif
            try
            {
                ExecuteImpl();
                return !Log.HasLoggedErrors;
            }
            finally
            {
#if NET461
                AssemblyResolution.Log = null;
#endif
            }
        }

        private void ExecuteImpl()
        {
            CorFlags parseFlags(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0;
                }

                if (Enum.TryParse<CorFlags>(value, out var result))
                {
                    return result;
                }

                Log.LogError($"Invalid flags: '{value}'");
                return 0;
            }

            var addFlags = parseFlags(AddFlags);
            var removeFlags = parseFlags(RemoveFlags);

            if (Log.HasLoggedErrors)
            {
                return;
            }

            using (var stream = File.Open(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using (var reader = new PEReader(stream))
            {
                var newFlags = (reader.PEHeaders.CorHeader.Flags & ~removeFlags) | addFlags;

                using (var writer = new BinaryWriter(stream))
                {
                    var mdReader = reader.GetMetadataReader();
                    stream.Position = reader.PEHeaders.CorHeaderStartOffset + OffsetFromStartOfCorHeaderToFlags;

                    writer.Write((int)newFlags);
                    writer.Flush();
                }
            }
        }
    }
}
