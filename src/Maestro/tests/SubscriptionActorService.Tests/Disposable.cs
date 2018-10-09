using System;

namespace SubscriptionActorService.Tests
{
    public class Disposable : IDisposable
    {
        public static IDisposable Create(Action onDispose)
        {
            return new Disposable(onDispose);
        }

        private readonly Action _dispose;

        private Disposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose();
        }
    }
}