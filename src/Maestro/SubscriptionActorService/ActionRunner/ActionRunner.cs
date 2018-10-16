// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Newtonsoft.Json;

namespace SubscriptionActorService
{
    public class ActionRunner : IActionRunner
    {
        private static readonly MethodInfo InvokeActionNoResultMethod =
            typeof(ActionRunner).GetRuntimeMethods().Single(m => m.Name == nameof(InvokeActionNoResult));

        private static readonly MethodInfo InvokeActionMethod =
            typeof(ActionRunner).GetRuntimeMethods().Single(m => m.Name == nameof(InvokeAction));

        public ActionRunner(ILogger<ActionRunner> logger)
        {
            Logger = logger;
        }

        protected ILogger Logger { get; }

        public Task<string> RunAction<T>(T tracker, string method, string arguments) where T : IActionTracker
        {
            IImmutableDictionary<string, ActionMethod> methods = ActionMethods.Get<T>();
            if (!methods.TryGetValue(method, out ActionMethod actionMethod))
            {
                throw new ArgumentException("Specified method not found.", nameof(method));
            }

            object[] args = actionMethod.DeserializeArguments(arguments);
            return (Task<string>) InvokeActionNoResultMethod.MakeGenericMethod(actionMethod.ResultType)
                .Invoke(this, new object[] {tracker, actionMethod, args});
        }

        public async Task<T> ExecuteAction<T>(Expression<Func<Task<ActionResult<T>>>> actionExpression)
        {
            if (!(actionExpression.Body is MethodCallExpression mce))
            {
                throw new ArgumentException("Expression must be a call expression.", nameof(actionExpression));
            }

            MethodInfo methodInfo = mce.Method;
            var target = (IActionTracker) GetValue(mce.Object);
            ActionMethod method = ActionMethods.Get(target.GetType())[methodInfo.Name];
            object[] arguments = GetArguments(mce.Arguments).ToArray();
            ActionResult<T> result = await InvokeAction<T>(target, method, arguments);
            if (result == null)
            {
                return default;
            }

            return result.Result;
        }

        private bool IsDisplayableException(Exception ex)
        {
            return ex is DarcException || ex is SubscriptionException;
        }

        private async Task<string> InvokeActionNoResult<T>(
            IActionTracker target,
            ActionMethod method,
            object[] arguments)
        {
            ActionResult<T> result = await InvokeAction<T>(target, method, arguments);
            return result?.Message ?? "";
        }

        private async Task<ActionResult<T>> InvokeAction<T>(
            IActionTracker target,
            ActionMethod method,
            object[] arguments)
        {
            object[]
                argumentsForFormat =
                    arguments.ToArray(); // copy the array because formatted log values modifies the array.
            string actionMessage = new FormattedLogValues(method.MessageFormat, argumentsForFormat).ToString();

            using (Logger.BeginScope(method.MessageFormat, argumentsForFormat))
            {
                try
                {
                    ActionResult<T> result = await (Task<ActionResult<T>>) method.MethodInfo.Invoke(target, arguments);
                    await target.TrackSuccessfulAction(actionMessage, result.Message);
                    return result;
                }
                catch (Exception displayable) when (IsDisplayableException(displayable))
                {
                    await target.TrackFailedAction(
                        actionMessage,
                        displayable.Message,
                        method.Name,
                        JsonConvert.SerializeObject(arguments));
                }
                catch (Exception ex)
                {
                    string message = $"Unexpected error processing action: {ex.Message}";
                    Logger.LogError(ex, message);
                    await target.TrackFailedAction(
                        actionMessage,
                        message,
                        method.Name,
                        JsonConvert.SerializeObject(arguments));
                }
            }

            return default;
        }

        private IEnumerable<object> GetArguments(IEnumerable<Expression> arguments)
        {
            foreach (Expression argument in arguments)
            {
                yield return GetValue(argument);
            }
        }

        private object GetValue(Expression argument)
        {
            switch (argument.NodeType)
            {
                case ExpressionType.Constant when argument is ConstantExpression ce:
                    return ce.Value;
                case ExpressionType.MemberAccess when argument is MemberExpression me:
                    object target = GetValue(me.Expression);
                    MemberInfo member = me.Member;
                    if (member is FieldInfo fi)
                    {
                        return fi.GetValue(target);
                    }

                    if (member is PropertyInfo pi)
                    {
                        return pi.GetValue(target);
                    }

                    break;
            }

            throw new NotImplementedException();
        }
    }
}
