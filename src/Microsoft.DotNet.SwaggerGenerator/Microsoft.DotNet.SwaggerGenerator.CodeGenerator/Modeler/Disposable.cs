using System;

namespace Microsoft.DotNet.SwaggerGenerator.Modeler
{
    public class Disposable : IDisposable
    {
        private readonly Action _onDispose;

        private Disposable(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }

        public static IDisposable Create(Action onDispose)
        {
            if (onDispose == null)
            {
                throw new ArgumentNullException(nameof(onDispose));
            }

            return new Disposable(onDispose);
        }
    }
}
