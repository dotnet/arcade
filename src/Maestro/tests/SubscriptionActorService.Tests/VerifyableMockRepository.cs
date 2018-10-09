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
