namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinitionWithTargetQueue
    {
        IJobDefinition WithTargetQueue(string queueId);
    }
}
