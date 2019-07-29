namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinitionWithType
    {
        IJobDefinitionWithTargetQueue WithType(string type);
    }
}
