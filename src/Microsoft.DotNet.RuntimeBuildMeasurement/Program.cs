using Microsoft.DotNet.RuntimeBuildMeasurement.Executors;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RuntimeBuildMeasurement
{
    public static class Program
    {
        static Task Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<IList<string>>(
                    "--subsets",
                    new List<string> { "Clr.Runtime", "Mono.Runtime", "libs.pretest(clr)" },
                    "Subsets to build. If any subset needs other subset need to be built before measurement, specify it like 'libs.pretest(clr)' (meaning 'measure libs.pretest build times, make sure clr has been build before the measurement happens')"),

                new Option<DirectoryInfo>(
                    "--working-directory",
                    OsPlatformHelper.GetPlatformDefaultDirectory(),
                    "Working directory."),

                new Option<bool>(
                    "--inspect-binlog",
                    true,
                    "Go through binlog to obtain more data."),

                new Option<bool>(
                    "--inspect-ninjalog",
                    true,
                    "Go through ninja log to obtain more data"),

                new Option<string>(
                    "--branch",
                    "origin/main",
                    "Branch or commit (history) to investigate"),

                new Option<int>(
                    "--noOfMeasurements",
                    5,
                    "Number of measurements to do in the past"),

                new Option<int>(
                    "--interval",
                    1,
                    "Number of days between measurements"),

                new Option<bool>(
                    "--publish-to-kusto",
                    false,
                    "Publish to Kusto using --cluster-url, --tenant-id, --database-name, --client-id and --client-secret credentials"),

                new Option<bool>(
                    "--publish-to-csv",
                    true,
                    "Publish to CSV using standard output"),

                new Option<string>(
                    "--cluster-url",
                    "",
                    "URL of the ingest endpoint of the Kusto cluster"),

                new Option<string>(
                    "--tenant-id",
                    "",
                    "Tenant ID"),

                new Option<string>(
                    "--database-name",
                    "",
                    "Name of the Kusto database to publish to"),

                new Option<string>(
                    "--client-id",
                    "",
                    "Client ID for Kusto auth."),

                new Option<string>(
                    "--client-secret",
                    "",
                    "Client secret for Kusto auth."),

                new Option<bool>(
                    "--skip-actual-measurement",
                    false,
                    "A form of dry run that will work with the repository (fetch, checkout, etc.), but won't trigger long running operations.")
            };

            rootCommand.Description = "A tool to measure build time of runtime repo.";

            rootCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<Args>(Compose);

            return rootCommand.InvokeAsync(args);
        }

        static Task Compose(Args args)
            => new Core(
                args.SkipActualMeasurement ? new MeasurementSkippingDecorator(new ProcessStartExecutor()) : new ProcessStartExecutor(),
                new Inspectors.BinLogInspector(), new Inspectors.NinjaLogInspector(),
                new Publishers.CsvPublisher(), new Publishers.KustoPublisher(args.PublishConfiguration.KustoCredentials))
            .Measure(
                args.Subsets.Select(Subset.Parse).ToList(),
                args.WorkingDirectory, args.InspectionConfiguration, args.PublishConfiguration,
                args.Branch, args.NoOfMeasurements, args.Interval);
    }
}
