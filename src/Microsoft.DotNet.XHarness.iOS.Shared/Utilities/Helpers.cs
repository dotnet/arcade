// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

public interface IHelpers
{
    string GetTerminalName(int filedescriptor);

    Guid GenerateStableGuid(string seed = null);

    Guid GenerateGuid();

    string Timestamp { get; }

    IEnumerable<IPAddress> GetLocalIpAddresses();
}

public class Helpers : IHelpers
{
    // We want guids that nobody else has, but we also want to generate the same guid
    // on subsequent invocations (so that csprojs don't change unnecessarily, which is
    // annoying when XS reloads the projects, and also causes unnecessary rebuilds).
    // Nothing really breaks when the sequence isn't identical from run to run, so
    // this is just a best minimal effort.
    private static readonly Random s_guidGenerator = new(unchecked((int)0xdeadf00d));
    public Guid GenerateStableGuid(string seed = null)
    {
        var bytes = new byte[16];
        if (seed == null)
        {
            s_guidGenerator.NextBytes(bytes);
        }
        else
        {
            using (var provider = SHA256.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(seed);
                var output = provider.ComputeHash(inputBytes);
                Array.Copy(output, bytes, 16);
            }
        }
        return new Guid(bytes);
    }

    public Guid GenerateGuid() => Guid.NewGuid();

    public string Timestamp => $"{DateTime.Now:yyyyMMdd_HHmmss}";

    [DllImport("/usr/lib/libc.dylib")]
    private static extern IntPtr ttyname(int filedes);

    public string GetTerminalName(int filedescriptor) => Marshal.PtrToStringAuto(ttyname(filedescriptor));

    public IEnumerable<IPAddress> GetLocalIpAddresses()
    {
        var ipv4Addresses = new List<IPAddress>();
        var otherAddresses = new List<IPAddress>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces().Where(adapter => adapter.OperationalStatus == OperationalStatus.Up))
        {
            var interfaceInfos = networkInterface.GetIPProperties().UnicastAddresses.Where(info => !IPAddress.IsLoopback(info.Address));
            foreach (UnicastIPAddressInformation info in interfaceInfos)
            {
                if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipv4Addresses.Add(info.Address);
                }
                else
                {
                    otherAddresses.Add(info.Address);
                }
            }
        }

        return ipv4Addresses.Concat(otherAddresses);
    }
}
