using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace Microsoft.DotNet.RuntimeBuildMeasurement
{
    public enum BuildType { Clean, NoOp };

    public enum OsPlatform { Windows, Linux, Mac };

    public record Subset(string Name, IList<string> Dependencies)
    {
        public static Subset Parse(string description)
        {
            Match match = Regex.Match(description, @"([^\(]*)\(?([^\)]*)\)?");

            string name = match.Groups[1].Value.Trim();
            List<string> deps = match.Groups[2].Value.Split(',').Select(dep => dep.Trim()).Where(dep => dep != "").ToList();

            return new Subset(name, deps);
        }
    }

    public record NamedDuration(string Name, TimeSpan Duration);

    public record Measurement(string Subset, string? Target, string? NinjaDir, BuildType BuildType, TimeSpan Duration);

    public record CsvMeasurement(
        string CommitHash, string CommitCreation, string MeasurementTimestamp,
        string Subset, string? Target, string? NinjaDir, BuildType BuildType, OsPlatform OsPlatform,
        double DurationInSeconds);

    public record InspectionConfiguration(
        bool InspectBinLog, bool InspectNinjaLog);

    public record PublishConfiguration(
        bool PublishToKusto, KustoCredentials KustoCredentials,
        bool PublishToCsv);

    public record KustoCredentials(
        string TenantId, Uri ClusterUrl,
        string ClientId, string ClientSecret, string DatabaseName);
}
