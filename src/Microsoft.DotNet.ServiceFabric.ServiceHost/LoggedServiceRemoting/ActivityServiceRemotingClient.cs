using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal class ActivityServiceRemotingClient : IServiceRemotingClient
    {
        internal readonly IServiceRemotingClient _inner;

        public ActivityServiceRemotingClient(IServiceRemotingClient inner)
        {
            _inner = inner;
        }

        public ResolvedServicePartition ResolvedServicePartition
        {
            get => _inner.ResolvedServicePartition;
            set => _inner.ResolvedServicePartition = value;
        }

        public string ListenerName
        {
            get => _inner.ListenerName;
            set => _inner.ListenerName = value;
        }

        public ResolvedServiceEndpoint Endpoint
        {
            get => _inner.Endpoint;
            set => _inner.Endpoint = value;
        }

        public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage)
        {
            var current = Activity.Current;
            if (current != null)
            {
                try
                {
                    var header = requestMessage.GetHeader();
                    header.AddHeader(ActivityServiceRemoting.OperationIdHeaderName,
                        Encoding.UTF8.GetBytes(current.Id));
                    if (current.Baggage.Any())
                    {
                        var baggageObject = current.Baggage.ToList();
                        var baggageStr =
                            JsonConvert.SerializeObject(baggageObject);
                        header.AddHeader(ActivityServiceRemoting.CorrelationContextHeaderName,
                            Encoding.UTF8.GetBytes(baggageStr));
                    }
                }
                catch (FabricElementAlreadyExistsException)
                {
                    // ignore
                }
            }
            return _inner.RequestResponseAsync(requestMessage);
        }

        public void SendOneWay(IServiceRemotingRequestMessage requestMessage)
        {
            _inner.SendOneWay(requestMessage);
        }
    }
}
