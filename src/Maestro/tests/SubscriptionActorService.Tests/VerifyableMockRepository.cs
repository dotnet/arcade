// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Moq;

namespace SubscriptionActorService.Tests
{
    public class VerifyableMockRepository : MockRepository
    {
        public VerifyableMockRepository(MockBehavior defaultBehavior) : base(defaultBehavior)
        {
        }

        public void VerifyNoUnverifiedCalls()
        {
            foreach (dynamic mock in Mocks)
            {
                mock.VerifyNoOtherCalls();
            }
        }
    }
}
