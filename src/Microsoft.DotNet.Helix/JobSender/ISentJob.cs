using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    public interface ISentJob
    {
        string CorrelationId { get; }
    }
}
