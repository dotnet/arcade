namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinitionWithTargetQueue
    {
        IJobDefinition WithTargetQueue(string queueId);
        IJobDefinition WithMultipleTargetQueues(params string[] queueIds);
    }
}
