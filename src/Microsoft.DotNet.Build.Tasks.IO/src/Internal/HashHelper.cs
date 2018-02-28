// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.IO.Internal
{
    internal class HashHelper
    {
        public static byte[] GetFileHash(string algorithmName, string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var algorithm = CreateAlgorithm(algorithmName))
            {
                return algorithm.ComputeHash(stream);
            }
        }

        public static string ConvertHashToHex(byte[] hash)
        {
            var sb = new StringBuilder();
            foreach (var b in hash)
            {
                sb.AppendFormat("{0:X2}", b);
            }

            return sb.ToString();
        }

        private static HashAlgorithm CreateAlgorithm(string algorithmName)
        {
            switch (algorithmName.ToUpperInvariant())
            {
                case "SHA256":
                    return new SHA256Managed();
                case "SHA384":
                    return new SHA384Managed();
                case "SHA512":
                    return new SHA512Managed();
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported hash algoritm {algorithmName}", nameof(algorithmName));
            }
        }
    }
}
