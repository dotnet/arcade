using System.Threading.Tasks;

namespace SubscriptionActorService
{
    public interface IActionTracker
    {
        Task TrackSuccessfulAction(string action, string result);

        Task TrackFailedAction(string action, string result, string method, string arguments);
    }
}
