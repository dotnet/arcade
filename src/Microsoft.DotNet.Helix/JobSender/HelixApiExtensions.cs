namespace Microsoft.DotNet.Helix.Client
{
    public static class HelixApiExtensions
    {
        public static IJobDefinitionWithSource Define(this IJob jobApi)
        {
            return new JobDefinition(jobApi);
        }
    }
}
