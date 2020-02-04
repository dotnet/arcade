using System.Net.Http.Headers;
using System.Reflection;
using System.Net;

namespace Microsoft.DotNet.Helix.Sdk
{
    public static class Helpers
    {
        public static ProductInfoHeaderValue UserAgentHeaderValue =>
            new ProductInfoHeaderValue(new ProductHeaderValue("HelixSdk", ProductVersion));

        public static string ProductVersion { get; } =
            typeof(Helpers).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        public static string CleanWorkItemName(string workItemName)
        {
            var decodedName = WebUtility.UrlDecode(workItemName);
            var convertedName = decodedName.Replace('/', '-');
            return convertedName;
        }
    }
}
