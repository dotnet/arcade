// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// The <see cref="SignTool"/> implementation used for test / validation runs.  Does not actually 
    /// change the sign state of the binaries.
    /// </summary>
    internal sealed class ValidationOnlySignTool : SignTool
    {
        internal bool TestSign { get; }

        internal ValidationOnlySignTool(SignToolArgs args, TaskLoggingHelper log) 
            : base(args, log)
        {
            TestSign = args.TestSign;
        }

        public override void RemovePublicSign(string assemblyPath)
        {
        }

        public override bool VerifySignedPEFile(Stream assemblyStream) 
            => true;

        public override bool RunMSBuild(IBuildEngine buildEngine, string projectFilePath, string binLogPath)
        {
            if (TestSign)
            {
                return buildEngine.BuildProjectFile(projectFilePath, null, null, null);
            }
            else
            {
                return true;
            }
        }
    }
}
