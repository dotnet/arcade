using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SubscriptionActorService
{
    public interface IActionRunner
    {
        Task<string> RunAction<T>(T tracker, string method, string arguments)
            where T : IActionTracker;

        Task<T> ExecuteAction<T>(Expression<Func<Task<ActionResult<T>>>> actionExpression);
    }
}
