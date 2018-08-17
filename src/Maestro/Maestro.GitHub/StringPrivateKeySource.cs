// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using GitHubJwt;

namespace Maestro.GitHub
{
    internal class StringPrivateKeySource : IPrivateKeySource
    {
        private readonly string _value;

        public StringPrivateKeySource(string value)
        {
            _value = value;
        }

        public TextReader GetPrivateKeyReader()
        {
            return new StringReader(_value);
        }
    }
}
