// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// The <see cref="SignTool"/> implementation used for test / validation runs.  Does not actually 
    /// change the sign state of the binaries.
    /// </summary>
    internal sealed class ValidationOnlySignTool : SignTool
    {
        internal ValidationOnlySignTool(SignToolArgs args) 
            : base(args)
        {
        }

        public override void RemovePublicSign(string assemblyPath)
        {
        }

        public override bool VerifySignedAssembly(Stream assemblyStream) 
            => true;

        public override bool RunMSBuild(IBuildEngine buildEngine, string projectFilePath)
            => true;
    }
}
