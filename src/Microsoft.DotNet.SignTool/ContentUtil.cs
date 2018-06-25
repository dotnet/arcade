// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SignTool
{
    internal sealed class ContentUtil
    {
        private readonly Dictionary<string, string> _filePathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly SHA256 _sha256 = SHA256.Create();

        internal string GetChecksum(Stream stream)
        {
            var hash = _sha256.ComputeHash(stream);
            return HashBytesToString(hash);
        }

        internal string GetChecksum(string filePath)
        {
            string checksum;
            if (!_filePathCache.TryGetValue(filePath, out checksum))
            {
                using (var stream = File.OpenRead(filePath))
                {
                    checksum = GetChecksum(stream);
                }
                _filePathCache[filePath] = checksum;
            }

            return checksum;
        }

        private string HashBytesToString(byte[] hash)
        {
            var data = BitConverter.ToString(hash);
            return data.Replace("-", "");
        }
    }
}
