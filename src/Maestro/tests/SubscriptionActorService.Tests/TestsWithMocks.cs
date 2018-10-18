// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Moq;

namespace SubscriptionActorService.Tests
{
    public class TestsWithMocks : IDisposable
    {
        private readonly VerifyableMockRepository _mocks;

        public TestsWithMocks()
        {
            _mocks = new VerifyableMockRepository(MockBehavior.Loose);
        }

        public virtual void Dispose()
        {
            _mocks.VerifyNoUnverifiedCalls();
        }

        protected Mock<T> CreateMock<T>() where T : class
        {
            return _mocks.Create<T>();
        }
    }
}
