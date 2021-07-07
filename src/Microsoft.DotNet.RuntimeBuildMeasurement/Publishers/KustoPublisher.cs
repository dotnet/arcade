using Kusto.Data;
using Kusto.Ingest;
using Microsoft.DotNet.Kusto;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Publishers
{
    public class KustoPublisher : IPublisher
    {
        private readonly KustoCredentials _kustoCredentials;

        public KustoPublisher(KustoCredentials kustoCredentials)
        {
            _kustoCredentials = kustoCredentials;
        }

        public Task Publish(IList<CsvMeasurement> measurements)
        {
            var kustoConnectionStringBuilderEngine = new KustoConnectionStringBuilder(_kustoCredentials.ClusterUrl.ToString())
                .WithAadApplicationKeyAuthentication(
                    applicationClientId: _kustoCredentials.ClientId,
                    applicationKey: _kustoCredentials.ClientSecret,
                    authority: _kustoCredentials.TenantId);

            using IKustoQueuedIngestClient kustoClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilderEngine);

            return KustoHelpers.WriteDataToKustoInMemoryAsync(
                kustoClient, _kustoCredentials.DatabaseName, "BuildTimeTelemetryTest",
                NullLogger.Instance, measurements, b => new[] {
                    new KustoValue(nameof(b.CommitHash), b.CommitHash, KustoDataTypes.String),
                    new KustoValue(nameof(b.CommitCreation), b.CommitCreation, KustoDataTypes.DateTime),
                    new KustoValue(nameof(b.MeasurementTimestamp), b.MeasurementTimestamp, KustoDataTypes.DateTime),
                    new KustoValue(nameof(b.Subset), b.Subset, KustoDataTypes.String),
                    new KustoValue(nameof(b.Target), b.Target ?? "", KustoDataTypes.String),
                    new KustoValue(nameof(b.NinjaDir), b.NinjaDir ?? "", KustoDataTypes.String),
                    new KustoValue(nameof(b.BuildType), b.BuildType.ToString(), KustoDataTypes.String),
                    new KustoValue(nameof(b.OsPlatform), b.OsPlatform.ToString(), KustoDataTypes.String),
                    new KustoValue(nameof(b.DurationInSeconds), b.DurationInSeconds.ToString(CultureInfo.InvariantCulture), "real")
            });
        }
    }
}
