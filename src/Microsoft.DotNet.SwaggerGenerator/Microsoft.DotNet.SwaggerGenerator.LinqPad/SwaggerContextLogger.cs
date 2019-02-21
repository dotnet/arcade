using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public class SwaggerContextLogger : ISwaggerContextLogger
    {
        private readonly TextWriter _output;

        public SwaggerContextLogger(TextWriter output)
        {
            _output = output;
        }

        private async Task Display(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, HttpContent content)
        {
            if (content != null)
            {
                headers = headers.Concat(content.Headers);
            }

            foreach (var (name, values) in headers)
            {
                if (name == "Authorization")
                {
                    await _output.WriteLineAsync($"{name}: *********");
                    continue;
                }
                foreach (var value in values)
                {
                    await _output.WriteLineAsync($"{name}: {value}");
                }
            }

            return;
        }

        public async Task RequestStarting(HttpRequestMessage request)
        {

            if (_output != null)
            {
                await _output.WriteLineAsync($"{request.Method} {request.RequestUri}");
                await Display(request.Headers, request.Content);
                await _output.WriteLineAsync();
            }
        }

        public async Task RequestFinished(HttpRequestMessage request, HttpResponseMessage response)
        {
            if (_output != null)
            {
                await _output.WriteLineAsync($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                await Display(response.Headers, response.Content);
                await _output.WriteLineAsync();
            }
        }
    }
}