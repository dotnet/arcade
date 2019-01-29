using System;
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

        public Task<string> UploadAsync(IBlobContainer payloadContainer, Action<string> log)
        {
            return _emptyStringTask;
        }

        public Task<Tuple<string, string>> UploadAsync(IBlobContainer payloadContainer, string destination, Action<string> log)
        {
            return Task.FromResult(new Tuple<string, string>(UploadAsync(payloadContainer, log).GetAwaiter().GetResult(), destination));
        }
    }
}
