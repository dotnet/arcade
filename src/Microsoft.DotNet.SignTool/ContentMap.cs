using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignTool
{
    /// <summary>
    /// Maintains the mapping between <see cref="FileName"/> instances and their checksum values
    /// </summary>
    internal sealed class ContentMap
    {
        private readonly Dictionary<string, FileName> _checksumMap = new Dictionary<string, FileName>(StringComparer.Ordinal);
        private readonly Dictionary<FileName, string> _fileNameMap = new Dictionary<FileName, string>();

        internal string GetChecksum(FileName fileName) => _fileNameMap[fileName];
        internal bool TryGetChecksum(FileName fileName, out string checksum) => _fileNameMap.TryGetValue(fileName, out checksum);
        internal FileName GetFileName(string checksum) => _checksumMap[checksum];
        internal bool TryGetFileName(string checksum, out FileName fileName) => _checksumMap.TryGetValue(checksum, out fileName);

        internal void Add(FileName fileName, string checksum)
        {
            _checksumMap.Add(checksum, fileName);
            _fileNameMap.Add(fileName, checksum);
        }
    }
}
