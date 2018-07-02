namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public interface IServiceContext
    {
        IServiceConfig Config { get; }
        bool IsServiceFabric { get; }
    }
}
