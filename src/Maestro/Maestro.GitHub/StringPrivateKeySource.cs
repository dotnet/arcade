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