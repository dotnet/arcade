using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Publishers
{
    interface IPublisher
    {
        Task Publish(IList<CsvMeasurement> measurements);
    }
}
