namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public interface IServiceConfigSection
    {
        string this[string name] { get; }
    }
}
