using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal class RichExceptionTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is ExceptionTelemetry exceptionTelemetry)
            {
                var ex = exceptionTelemetry.Exception;
                if (ex is Rest.HttpOperationException httpEx)
                {
                    var res = httpEx.Response;
                    exceptionTelemetry.Properties["statusCode"] = ((int)res.StatusCode).ToString();
                    var content = res.Content;
                    if (content.Length > 512)
                    {
                        content = content.Substring(0, 512);
                    }
                    exceptionTelemetry.Properties["responseText"] = content;
                }
            }
        }
    }
}