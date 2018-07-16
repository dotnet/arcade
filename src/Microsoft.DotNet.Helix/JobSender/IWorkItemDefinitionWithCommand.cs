namespace Microsoft.DotNet.Helix.Client
{
    public interface IWorkItemDefinitionWithCommand
    {
        IWorkItemDefinitionWithPayload WithCommand(string command);
    }
}
