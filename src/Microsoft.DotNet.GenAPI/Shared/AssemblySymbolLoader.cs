// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared
{
    internal class AssemblySymbolLoader : IAssemblySymbolLoader
    {
        public IAssemblySymbol? LoadAssembly(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return LoadAssembly(stream);
            }
        }

        public IAssemblySymbol? LoadAssembly(Stream stream)
        {
            PortableExecutableReference reference;

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                // MetadataReference.CreateFromStream closes the stream
                reference = MetadataReference.CreateFromStream(memoryStream);
            }

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
            var compilation = CSharpCompilation.Create($"AssemblyLoader_{DateTime.Now:MM_dd_yy_HH_mm_ss_FFF}", options: compilationOptions);

            /// TODO: add trusted platform assemblies ?

            return compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
        }
    }
}
