// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SubscriptionActorService
{
    public static class ActionResult
    {
        public static ActionResult<T> Create<T>(T result, string message)
        {
            return new ActionResult<T>(result, message);
        }
    }

    public class ActionResult<T>
    {
        public ActionResult(T result, string message)
        {
            Result = result;
            Message = message;
        }

        public T Result { get; }
        public string Message { get; }
    }
}
