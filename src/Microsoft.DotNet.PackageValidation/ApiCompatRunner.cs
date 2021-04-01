// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;

namespace Microsoft.DotNet.PackageValidation
{
    internal class ApiCompatRunner
    {
        private IEnumerable<IAssemblySymbol> leftSymbols, rightSymbols;

        public ApiCompatRunner(string leftAssemblyPath, string rightAssemblyPath)
        {
            leftSymbols = new AssemblyLoader().LoadAssemblies(new string[] { leftAssemblyPath });
            rightSymbols = new AssemblyLoader().LoadAssemblies(new string[] { rightAssemblyPath });
        }

        public IEnumerable<CompatDifference> RunApiCompat(string noWarn)
        {
            ApiDiffer differ = new();
            differ.NoWarn = noWarn;
            return  differ.GetDifferences(leftSymbols, rightSymbols);
        }
    }   
}
