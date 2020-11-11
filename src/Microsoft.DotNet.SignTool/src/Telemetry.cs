using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.SignTool
{
    internal class Telemetry
    {
        private static readonly string s_sendEventName = "SignTool task completed";
        private static Dictionary<string, double> s_metrics;
        private static readonly Dictionary<string, string> s_properties = new Dictionary<string, string>()
        {
            { "BUILD_REPOSITORY_URI", Environment.GetEnvironmentVariable("BUILD_REPOSITORY_URI") },
            { "BUILD_SOURCEBRANCH", Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH") },
            { "BUILD_BUILDNUMBER", Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER") },
            { "BUILD_SOURCEVERSION", Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION") }
        };

        private Telemetry()
        {}

        internal static void AddMetric(string name, double value)
        {
            if(s_metrics == null)
            {
                s_metrics = new Dictionary<string, double>();
            }
            s_metrics.Add(name, value);
        }

        public static void SendEvents()
        {
            if(s_metrics == null)
            {
                return;
            }
            // set APPINSIGHTS_INSTRUMENTATIONKEY environment variable to begin tracking telemetry
            TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();

            TelemetryClient telemetryClient = new TelemetryClient(configuration);

            telemetryClient.TrackEvent(s_sendEventName, properties: s_properties, metrics: s_metrics);
            telemetryClient.Flush();
            s_metrics = null;
        }
    }
}
