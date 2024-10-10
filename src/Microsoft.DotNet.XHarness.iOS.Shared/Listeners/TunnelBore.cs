using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Listeners;

/// <summary>
/// manages the tunnels that are used to communicate with the devices. We want to create a single tunnel per device
/// since we can only run one app per device, this should not be a problem.
///
/// definition:
/// A tunnel boring machine, also known as a "mole", is a machine used to excavate tunnels with a circular cross section
/// through a variety of soil and rock strata. They may also be used for microtunneling.
/// </summary>
public interface ITunnelBore : IAsyncDisposable
{

    // create a new tunnel for the device with the given name.
    ITcpTunnel Create(string device, ILog mainLog);

    // close a given tunnel
    Task Close(string device);
}

public class TunnelBore : ITunnelBore
{
    private readonly object _tunnelsLock = new();
    private readonly IMlaunchProcessManager _processManager;
    private readonly ConcurrentDictionary<string, TcpTunnel> _tunnels = new();

    public TunnelBore(IMlaunchProcessManager processManager)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
    }

    // Creates a new tcp tunnel to the given device that will use the port from the passed listener.
    public ITcpTunnel Create(string device, ILog mainLog)
    {
        lock (_tunnelsLock)
        {
            if (_tunnels.ContainsKey(device))
            {
                string msg = $"Cannot create more than one tunnel to device {device}";
                mainLog.WriteLine(msg);
                throw new InvalidOperationException(msg);
            }
            _tunnels[device] = new TcpTunnel(_processManager);
            return _tunnels[device];
        }
    }

    public async Task Close(string device)
    {
        // closes a tcp tunnel that was created for the given device.
        if (_tunnels.TryRemove(device, out var tunnel))
        {
            await tunnel.DisposeAsync(); // calls close already
        }
    }

    public async ValueTask DisposeAsync()
    {
        var devices = _tunnels.Keys.ToArray();
        foreach (var d in devices)
        {
            if (_tunnels.TryRemove(d, out var tunnel))
            {
                // blocking, but we are disposing
                await tunnel.DisposeAsync(); // alls close already
            }
        }
        GC.SuppressFinalize(this);
    }
}
