using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Darc
{
    public class LocalActions
    {
        private readonly DarcSettings darcSetings;

        public LocalActions(DarcSettings settings)
        {
            darcSetings = settings;
        }
    }
}
