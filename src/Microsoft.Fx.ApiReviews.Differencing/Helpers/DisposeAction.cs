// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Fx.ApiReviews.Differencing.Helpers
{
    internal sealed class DisposeAction : IDisposable
    {
        private Action _action;

        public DisposeAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            if (_action == null)
                return;

            _action();
            _action = null;
        }
    }
}
