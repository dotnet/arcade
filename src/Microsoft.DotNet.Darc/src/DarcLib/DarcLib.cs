using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Darc
{
    public class DarcLib
    {
        public RemoteActions RemoteAction { get; set; }

        public LocalActions LocalAction { get; set; }

        public DarcLib() : this(null)
        {

        }

        public DarcLib(DarcSettings settings)
        {
            RemoteAction = new RemoteActions(settings);
            LocalAction = new LocalActions(settings);
        }
    }
}
