using System.Xml.Linq;
using LINQPad.Extensibility.DataContext;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public class SwaggerProperties
    {
        private IConnectionInfo _cxInfo;
        private XElement _driverData;

        public SwaggerProperties(IConnectionInfo info)
        {
            _cxInfo = info;
            _driverData = info.DriverData;
        }

        public string Uri
        {
            get => (string) _driverData.Element("Uri") ?? "";
            set => _driverData.SetElementValue("Uri", value);
        }
    }
}