// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public static class ActivityServiceRemoting
    {
        public const string OperationIdHeaderName = "X-Operation-Id";
        public const string CorrelationContextHeaderName = "X-Correlation-Context";
    }

    public class ActivityServiceRemotingMessageDispatcher : ServiceRemotingMessageDispatcher
    {
        public ActivityServiceRemotingMessageDispatcher(
            ServiceContext serviceContext,
            IService serviceImplementation,
            IServiceRemotingMessageBodyFactory serviceRemotingMessageBodyFactory) : base(
            serviceContext,
            serviceImplementation,
            serviceRemotingMessageBodyFactory)
        {
        }

        public override async Task<IServiceRemotingResponseMessage> HandleRequestResponseAsync(
            IServiceRemotingRequestContext requestContext,
            IServiceRemotingRequestMessage requestMessage)
        {
            IServiceRemotingRequestMessageHeader headers = requestMessage.GetHeader();
            if (headers.TryGetHeaderValue(ActivityServiceRemoting.OperationIdHeaderName, out byte[] operationIdBytes))
            {
                string operationId = Encoding.UTF8.GetString(operationIdBytes);
                Activity activity = null;
                try
                {
                    activity = new Activity("Microsoft.ServiceFabric.Remoting.RPCInvoke");
                    activity.SetParentId(operationId);
                    if (headers.TryGetHeaderValue(
                        ActivityServiceRemoting.CorrelationContextHeaderName,
                        out byte[] correlationContextBytes))
                    {
                        string correlationContextStr = Encoding.UTF8.GetString(correlationContextBytes);
                        var baggage =
                            JsonConvert.DeserializeObject<IList<KeyValuePair<string, string>>>(correlationContextStr);
                        foreach ((string key, string value) in baggage)
                        {
                            activity.AddBaggage(key, value);
                        }
                    }

                    activity.Start();
                    return await base.HandleRequestResponseAsync(requestContext, requestMessage);
                }
                finally
                {
                    activity?.Stop();
                }
            }

            return await base.HandleRequestResponseAsync(requestContext, requestMessage);
        }
    }
}
