using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class EmptyPayload : IPayload
    {
        private readonly Task<string> _emptyStringTask = Task.FromResult("");

        private EmptyPayload()
        {
        }

        public static IPayload Instance { get; } = new EmptyPayload();

        public Task<string> UploadAsync(IBlobContainer payloadContainer)
        {
            return _emptyStringTask;
        }
    }
}
