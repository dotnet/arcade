// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.SignTool
{
    internal sealed class FakeSignTool : SignTool
    {
        private static readonly byte[] _stamp = Guid.NewGuid().ToByteArray();

        internal FakeSignTool(SignToolArgs args)
            : base(args)
        {
        }

        public override void RemovePublicSign(string assemblyPath)
        {
        }

        public override bool VerifySignedPEFile(Stream stream)
        {
            var buffer = new byte[_stamp.Length];
            return stream.TryReadAll(buffer, 0, buffer.Length) == buffer.Length && 
                   ByteSequenceComparer.Equals(buffer, _stamp);
        }

        public override bool RunMSBuild(IBuildEngine buildEngine, string projectFilePath, string binLogPath)
            => buildEngine.BuildProjectFile(projectFilePath, null, null, null);

        internal static void SignFile(string path)
        {
            if (FileSignInfo.IsPEFile(path))
            {
                SignPEFile(path);
            }
        }

        private static void SignPEFile(string path)
        {
            using (var stream = File.OpenWrite(path))
            {
                stream.Write(_stamp, 0, _stamp.Length);
            }
        }
    }
}
