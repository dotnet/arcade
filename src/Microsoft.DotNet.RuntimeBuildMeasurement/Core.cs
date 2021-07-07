using Microsoft.DotNet.RuntimeBuildMeasurement.Executors;
using Microsoft.DotNet.RuntimeBuildMeasurement.Inspectors;
using Microsoft.DotNet.RuntimeBuildMeasurement.Publishers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RuntimeBuildMeasurement
{
    public class Core
    {
        private readonly IExecutor _executor;

        private readonly BinLogInspector _binLogInspector;
        private readonly NinjaLogInspector _ninjaLogInspector;

        private readonly CsvPublisher _csvPublisher;
        private readonly KustoPublisher _kustoPublisher;

        public Core(
            IExecutor executor,
            BinLogInspector binLogInspector, NinjaLogInspector ninjaLogInspector,
            CsvPublisher csvPublisher, KustoPublisher kustoPublisher)
        {
            _executor = executor;

            _binLogInspector = binLogInspector;
            _ninjaLogInspector = ninjaLogInspector;

            _csvPublisher = csvPublisher;
            _kustoPublisher = kustoPublisher;
        }

        public async Task Measure(
            IList<Subset> subsets, DirectoryInfo workingDirectory,
            InspectionConfiguration inspectionConfiguration, PublishConfiguration publishConfiguration,
            string branch, int noOfMeasurements, int interval)
        {
            await EnsureRepoPresence(workingDirectory);

            foreach (string commitHash in await GetCommitHashes(workingDirectory, branch, noOfMeasurements, interval))
            {
                try
                {
                    var measurements = new List<CsvMeasurement>();

                    await EnsureRepoState(workingDirectory, commitHash);

                    await Warmup(workingDirectory);

                    foreach (Subset subset in subsets)
                    {
                        await EnsureRepoState(workingDirectory, commitHash);

                        foreach (string subsetDependency in subset.Dependencies)
                        {
                            await _executor.ExecuteRepoCommand(workingDirectory, OsPlatformHelper.GetBuildCommand(), subsetDependency);
                        }

                        measurements.AddRange(
                            await ContextualizeMeasurements(
                                workingDirectory,
                                await MeasureSubset(workingDirectory, subset, BuildType.Clean, inspectionConfiguration),
                                commitHash));

                        measurements.AddRange(
                            await ContextualizeMeasurements(
                                workingDirectory,
                                await MeasureSubset(workingDirectory, subset, BuildType.NoOp, inspectionConfiguration),
                                commitHash));
                    }

                    if (publishConfiguration.PublishToCsv)
                    {
                        await _csvPublisher.Publish(measurements);
                    }

                    if (publishConfiguration.PublishToKusto)
                    {
                        await _kustoPublisher.Publish(measurements);
                    }
                }
#pragma warning disable CA1031 // this is the one place in the application where we want to catch general exceptions
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    Console.WriteLine(ex);
                }
            }
        }

        private async Task<IList<string>> GetCommitHashes(DirectoryInfo workingDirectory, string branch, int noOfMeasurements, int interval)
        {
            var commitHashes = new List<string>();

            for (int i = 0; i < noOfMeasurements; i++)
            {
                commitHashes.Add((await _executor.Git(workingDirectory, $"rev-list -n 1 --first-parent --before=\"{DateTime.Now.AddDays(-1 * interval * i)}\" {branch}")).Trim());
            }

            return commitHashes;
        }

        private async Task<IList<CsvMeasurement>> ContextualizeMeasurements(
            DirectoryInfo workingDirectory, IList<Measurement> measurements, string commitHash)
        {
            DateTime commitDate = DateTime.Parse(await _executor.Git(workingDirectory, $"show -s --format=%ci {commitHash}"), CultureInfo.InvariantCulture);

            return measurements.Select(m => new CsvMeasurement(
                commitHash,
                commitDate.ToString("s", CultureInfo.InvariantCulture),
                DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture),
                m.Subset, m.Target, m.NinjaDir,
                m.BuildType,
                OsPlatformHelper.GetCurrentPlatform(),
                m.Duration.TotalSeconds))
            .ToList();
        }

        private async Task EnsureRepoPresence(DirectoryInfo workingDirectory)
        {
            if (!Directory.Exists(Path.Combine(workingDirectory.FullName, ".git")))
            {
                Console.WriteLine($"Cloning dotnet/runtime into {workingDirectory}");

                await _executor.Execute(workingDirectory, "mkdir", workingDirectory.FullName);
                await _executor.Git(workingDirectory, $"clone https://github.com/dotnet/runtime.git {workingDirectory}");
            }
            else
            {
                Console.WriteLine($"dotnet/runtime already exists at {workingDirectory}, good, just fetching the current version");

                await _executor.Git(workingDirectory, "fetch");
            }
        }

        /// <summary>
        /// Clean the repo from possible remnants of previous work.
        /// </summary>
        private async Task EnsureRepoState(DirectoryInfo workingDirectory, string commitHash)
        {
            await _executor.Git(workingDirectory, "clean -xdf -e .dotnet");
            await _executor.Git(workingDirectory, "reset --hard HEAD");
            await _executor.Git(workingDirectory, $"checkout {commitHash}");
        }

        /// <summary>
        /// Performance might get affected by disk caching. This command will try its best to clean them.
        /// </summary>
        private async Task ClearDiskCaches(DirectoryInfo workingDirectory)
        {
            if (OsPlatformHelper.GetCurrentPlatform() == OsPlatform.Linux)
            {
                await _executor.Execute(workingDirectory, "sync");
                await _executor.Execute(workingDirectory, "sudo", "sh -c \"/usr/bin/echo 3 > /proc/sys/vm/drop_caches\"");
            }
        }

        /// <summary>
        /// Downloads and extracts .NET SDK into .dotnet folder
        /// </summary>
        private Task Warmup(DirectoryInfo workingDirectory)
        {
            return _executor.ExecuteRepoCommand(workingDirectory, OsPlatformHelper.GetBuildCommand(), "--help");
        }

        /// <summary>
        /// Actual measurement of subset build performance.
        /// </summary>
        private async Task<IList<Measurement>> MeasureSubset(
            DirectoryInfo workingDirectory, Subset subset, BuildType buildType,
            InspectionConfiguration inspectionConfiguration)
        {
            await ClearDiskCaches(workingDirectory);

            TimeSpan timeToCompletion = await _executor.MeasureRepoCommandDuration(
                workingDirectory, OsPlatformHelper.GetBuildCommand(), $"{subset.Name} -bl");

            var result = new List<Measurement>
            {
                new Measurement(subset.Name, null, null, buildType, timeToCompletion)
            };

            if (inspectionConfiguration.InspectBinLog)
            {
                string binLogPath = Path.Combine(workingDirectory.FullName, "artifacts/log/Debug/Build.binlog");
                if (File.Exists(binLogPath))
                {
                    IEnumerable<NamedDuration> targetTimes = _binLogInspector.Inspect(new FileInfo(binLogPath));

                    foreach (NamedDuration targetTime in targetTimes)
                    {
                        result.Add(new Measurement(subset.Name, targetTime.Name, null, buildType, targetTime.Duration));
                    }
                }
            }

            if (inspectionConfiguration.InspectNinjaLog)
            {
                foreach (string ninjaLog in Directory.GetFiles(workingDirectory.FullName, ".ninja_log"))
                {
                    Console.WriteLine($"Discovered .ninja_log file: {ninjaLog}");

                    foreach (NamedDuration ninjaTime in _ninjaLogInspector.Inspect(new FileInfo(ninjaLog)))
                    {
                        result.Add(new Measurement(subset.Name, null, ninjaTime.Name, buildType, ninjaTime.Duration));
                    }
                }
            }

            return result;
        }
    }
}