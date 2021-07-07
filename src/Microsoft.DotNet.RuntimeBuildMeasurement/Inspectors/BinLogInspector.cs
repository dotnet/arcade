using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Inspectors
{
    public class BinLogInspector : IInspector
    {
        private readonly TimeSpan TargetDurationThreshhold = TimeSpan.FromSeconds(1.0);

        public IEnumerable<NamedDuration> Inspect(FileInfo file)
        {
            using FileStream buildBinLogStream = file.OpenRead();
            Build.Logging.StructuredLogger.Build buildBinLog = Build.Logging.StructuredLogger.Serialization.ReadBinLog(buildBinLogStream);
            return GetDurations(buildBinLog);
        }

        private IEnumerable<NamedDuration> GetDurations(Build.Logging.StructuredLogger.TimedNode node)
        {
            if (node.Duration > TargetDurationThreshhold)
            {
                if (node.TypeName == "Target")
                {
                    Console.WriteLine($">> {node.Name} (of type {node.TypeName}): {node.Duration.TotalSeconds} s");
                    yield return new NamedDuration(node.Name, node.Duration);
                }
            }
            else
            {
                yield break;
            }

            foreach (Build.Logging.StructuredLogger.TimedNode child in node.Children.OfType<Build.Logging.StructuredLogger.TimedNode>())
            {
                foreach (NamedDuration duration in GetDurations(child))
                {
                    yield return duration;
                }
            }
        }
    }
}
