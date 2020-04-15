// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Fx.Csv
{
    internal sealed class CsvDocumentReader : CsvReader
    {
        private IList<string> _keys;
        private IList<IDictionary<string, string>> _rows;
        private int _currentRow;

        public CsvDocumentReader(IList<string> keys, IList<IDictionary<string, string>> rows)
            : base(CsvSettings.Default)
        {
            _keys = keys;
            _rows = rows;
        }

        public override IEnumerable<string> Read()
        {
            if (_currentRow >= _rows.Count)
                return null;

            var row = _rows[_currentRow];
            var result = new string[_keys.Count];
            for (var i = 0; i < _keys.Count; i++)
            {
                var key = _keys[i];
                result[i] = row[key];
            }
            _currentRow++;
            return result;
        }
    }
}
