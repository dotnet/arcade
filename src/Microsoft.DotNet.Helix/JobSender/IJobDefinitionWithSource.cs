namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinitionWithSource
    {
        IJobDefinitionWithType WithSource(string source);
    }
}
