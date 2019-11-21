namespace Microsoft.DotNet.Helix.Client
{
    /// <summary>
    /// Work item definition that lacks required information about
    /// what command to run.
    /// </summary>
    public interface IWorkItemDefinitionWithCommand
    {
        /// <summary>
        /// Specify the actual command to be processed as your job
        /// on one of the machines of the queue you selected.
        /// </summary>
        /// <param name="command">
        /// Any command (like `ls` or `dotnet test Solution.sln`) you want
        /// to be processed.
        /// </param>
        IWorkItemDefinitionWithPayload WithCommand(string command);
    }
}
