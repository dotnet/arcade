using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.RuntimeBuildMeasurement
{
    class Args
    {
#pragma warning disable CS8618 // null checking doesn't make much sense for this auto-filled DTO
        public IList<string> Subsets { get; set; }
        public DirectoryInfo WorkingDirectory { get; set; }
        public bool InspectBinlog { get; set; }
        public bool InspectNinjaLog { get; set; }
        public string Branch { get; set; }
        public int NoOfMeasurements { get; set; }
        public int Interval { get; set; }

        public bool PublishToKusto { get; set; }
        public bool PublishToCsv { get; set; }

        public string ClusterUrl { get; set; }
        public string TenantId { get; set; }
        public string DatabaseName { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        public bool SkipActualMeasurement { get; set; }
#pragma warning restore CS8618

        public InspectionConfiguration InspectionConfiguration =>
            new(InspectBinlog, InspectNinjaLog);

        public PublishConfiguration PublishConfiguration =>
            new(PublishToKusto, KustoCredentials, PublishToCsv);

        public KustoCredentials KustoCredentials =>
            new(TenantId, new Uri(ClusterUrl), ClientId, ClientSecret, DatabaseName);
    }
}
