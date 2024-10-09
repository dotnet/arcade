// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Listeners;

public enum ListenerTransport
{
    Tcp,
    Http,
    File,
}

public interface ISimpleListenerFactory
{
    (ListenerTransport transport, ISimpleListener listener, string listenerTempFile) Create(
        RunMode mode,
        ILog log,
        IFileBackedLog testLog,
        bool isSimulator,
        bool autoExit,
        bool xmlOutput);

    ITunnelBore TunnelBore { get; }
    bool UseTunnel { get; }
}

public class SimpleListenerFactory : ISimpleListenerFactory
{

    public ITunnelBore TunnelBore { get; private set; }

    public bool UseTunnel => TunnelBore != null;

    public SimpleListenerFactory(ITunnelBore tunnelBore = null) =>
        TunnelBore = tunnelBore; // allow it to be null in case we are working with a sim

    public (ListenerTransport transport, ISimpleListener listener, string listenerTempFile) Create(
        RunMode mode,
        ILog log,
        IFileBackedLog testLog,
        bool isSimulator,
        bool autoExit,
        bool xmlOutput)
    {
        string listenerTempFile = null;
        ISimpleListener listener;
        ListenerTransport transport;

        if (mode == RunMode.WatchOS)
        {
            transport = isSimulator ? ListenerTransport.File : ListenerTransport.Http;
        }
        else
        {
            transport = ListenerTransport.Tcp;
        }

        switch (transport)
        {
            case ListenerTransport.File:
                listenerTempFile = testLog.FullPath + ".tmp";
                listener = new SimpleFileListener(listenerTempFile, log, testLog, xmlOutput);
                break;
            case ListenerTransport.Http:
                listener = new SimpleHttpListener(log, testLog, autoExit);
                break;
            case ListenerTransport.Tcp:
                listener = new SimpleTcpListener(log, testLog, autoExit, UseTunnel);
                break;
            default:
                throw new NotImplementedException("Unknown type of listener");
        }

        return (transport, listener, listenerTempFile);
    }
}
