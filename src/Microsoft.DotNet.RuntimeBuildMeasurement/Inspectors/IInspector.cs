using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Inspectors
{
    interface IInspector
    {
        IEnumerable<NamedDuration> Inspect(FileInfo file);
    }
}
