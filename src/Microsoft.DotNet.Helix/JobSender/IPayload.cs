using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal interface IPayload
    {
        Task<string> UploadAsync(IBlobContainer payloadContainer, Action<string> log);
        Task<Tuple<string, string>> UploadAsync(IBlobContainer payloadContainer, string destination, Action<string> log);
    }
}
