using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal interface IPayload
    {
        Task<string> UploadAsync(IBlobContainer payloadContainer);
    }
}
