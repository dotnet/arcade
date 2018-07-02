namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public interface IServiceConfig
    {
        IServiceConfigSection this[string name] { get; }
    }
}
