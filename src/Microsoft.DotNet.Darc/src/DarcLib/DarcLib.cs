using System;
using System.Collections.Generic;
using System.Text;

/*
 Prototype class. We'll need to: 
    *  Based on maybe settings initialize the "type" of Darc we'll use so we don't initialize things that we won't use
     */
namespace Microsoft.DotNet.Darc
{
    public class DarcLib
    {
        public RemoteActions RemoteAction { get; set; }

        public LocalActions LocalAction { get; set; }

        public DarcLib() : this(null) { }

        public DarcLib(DarcSettings settings)
        {
            RemoteAction = new RemoteActions(settings);
        }
    }
}
