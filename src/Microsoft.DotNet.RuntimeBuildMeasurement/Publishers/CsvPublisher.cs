using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Publishers
{
    public class CsvPublisher : IPublisher
    {
        public async Task Publish(IList<CsvMeasurement> measurements)
        {
            var sw = new StringWriter();
            await new CsvWriter(sw, CultureInfo.InvariantCulture).WriteRecordsAsync(measurements);
            Console.WriteLine(sw.ToString());
        }
    }
}
