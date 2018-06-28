namespace Microsoft.DotNet.Helix.Client
{
    public interface IWorkItemDefinition
    {
        string WorkItemName { get; }
        string Command { get; }
    }
}
