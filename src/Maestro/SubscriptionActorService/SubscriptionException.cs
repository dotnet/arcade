// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace SubscriptionActorService
{
    /// <summary>
    ///     Exception thrown when there is a failure updating a subscription that should be surfaced to users.
    /// </summary>
    public class SubscriptionException : Exception
    {
        public SubscriptionException(string message) : this(message, null)
        {
        }

        public SubscriptionException() : this("There was a problem updating the subscription.")
        {
        }

        protected SubscriptionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public SubscriptionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
