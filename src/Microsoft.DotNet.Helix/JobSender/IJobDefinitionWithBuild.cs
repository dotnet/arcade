namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinitionWithBuild
    {
        IJobDefinitionWithTargetQueue WithBuild(string buildNumber);
    }
}
