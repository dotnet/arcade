namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinitionWithType
    {
        IJobDefinitionWithBuild WithType(string type);
    }
}
