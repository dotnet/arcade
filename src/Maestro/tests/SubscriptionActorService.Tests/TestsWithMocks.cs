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

        protected Mock<T> CreateMock<T>()
            where T : class
        {
            return _mocks.Create<T>();
        }

        public virtual void Dispose()
        {
            _mocks.VerifyNoUnverifiedCalls();
        }
    }
}
