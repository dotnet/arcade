using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public interface ISwaggerContextLogger
    {
        Task RequestStarting(HttpRequestMessage request);
        Task RequestFinished(HttpRequestMessage request, HttpResponseMessage response);
    }
}