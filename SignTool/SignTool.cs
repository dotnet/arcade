// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignTool
{
    internal interface ISignTool
    {
        void RemovePublicSign(string assemblyPath);

        bool VerifySignedAssembly(Stream assemblyStream);

        void Sign(IEnumerable<FileSignInfo> filesToSign);
    }

    internal static partial class SignToolFactory
    {
        internal static ISignTool Create(SignToolArgs args)
        {
            if (args.Test)
            {
                return new TestSignTool(args);
            }

            return new RealSignTool(args);
        }
    }
}
