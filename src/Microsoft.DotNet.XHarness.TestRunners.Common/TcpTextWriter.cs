// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// Class that writes output into a TCP connection.
/// It is an adaptation of NUnitLite's TcpWriter.cs with additional overrides and with network-activity UI enhancement
/// This code is a small modification of https://github.com/spouliot/Touch.Unit/blob/master/NUnitLite/TouchRunner/TcpTextWriter.cs
/// </summary>
internal class TcpTextWriter : TextWriter
{
    private static readonly TimeSpan s_connectionAwaitPeriod = TimeSpan.FromMinutes(1);

    private readonly StreamWriter _writer;

    private TcpTextWriter(StreamWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public static TcpTextWriter InitializeWithTunnelConnection(int port)
    {
        ValidatePort(port);

        var server = new TcpListener(IPAddress.Any, port);
        server.Server.ReceiveTimeout = 5000;
        server.Start();
        var watch = Stopwatch.StartNew();

        while (!server.Pending())
        {
            if (watch.Elapsed > s_connectionAwaitPeriod)
            {
                throw new Exception($"No inbound TCP connection after {(int)s_connectionAwaitPeriod.TotalSeconds} seconds");
            }

            Thread.Sleep(100);
        }

        var client = server.AcceptTcpClient();

        // Block until we have the ping from the client side
        byte[] buffer = new byte[16 * 1024];
        var stream = client.GetStream();
        while ((_ = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            var message = Encoding.UTF8.GetString(buffer);
            if (message.Contains("ping"))
            {
                break;
            }
        }

        var writer = new StreamWriter(client.GetStream());

        return new TcpTextWriter(writer);
    }

    public static TcpTextWriter InitializeWithDirectConnection(string hostName, int port)
    {
        if (hostName is null)
        {
            throw new ArgumentNullException(nameof(hostName));
        }

        ValidatePort(port);

        hostName = SelectHostName(hostName.Split(','), port);

        var client = new TcpClient(hostName, port);
        var writer = new StreamWriter(client.GetStream());

        return new TcpTextWriter(writer);
    }

    // we override everything that StreamWriter overrides from TextWriter

    public override Encoding Encoding => Encoding.UTF8;

    public override void Close()
    {
        _writer.Close();
    }

    protected override void Dispose(bool disposing) => _writer?.Dispose();

    public override void Flush()
    {
        _writer.Flush();
    }

    // minimum to override - see http://msdn.microsoft.com/en-us/library/system.io.textwriter.aspx
    public override void Write(char value)
    {
        _writer.Write(value);
    }

    public override void Write(char[]? buffer)
    {
        _writer.Write(buffer);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        _writer.Write(buffer, index, count);
    }

    public override void Write(string? value)
    {
        _writer.Write(value);
    }

    // special extra override to ensure we flush data regularly

    public override void WriteLine()
    {
        _writer.WriteLine();
        _writer.Flush();
    }

    private static string SelectHostName(string[] names, int port)
    {
        if (names.Length == 1)
        {
            return names[0];
        }

        object lock_obj = new object();
        string? result = null;
        int failures = 0;

        using (var evt = new ManualResetEvent(false))
        {
            for (int i = names.Length - 1; i >= 0; i--)
            {
                var name = names[i];
                ThreadPool.QueueUserWorkItem((v) =>
                {
                    try
                    {
                        var client = new TcpClient(name, port);
                        using (var writer = new StreamWriter(client.GetStream()))
                        {
                            writer.WriteLine("ping");
                        }
                        lock (lock_obj)
                        {
                            if (result == null)
                            {
                                result = name;
                            }
                        }
                        evt.Set();
                    }
                    catch (Exception)
                    {
                        lock (lock_obj)
                        {
                            failures++;
                            if (failures == names.Length)
                            {
                                evt.Set();
                            }
                        }
                    }
                });
            }

            // Wait for 1 success or all failures
            evt.WaitOne();
        }

        if (result == null)
        {
            throw new InvalidOperationException("Couldn't connect to any of the hostnames.");
        }

        return result;
    }

    private static void ValidatePort(int port)
    {
        if (port < 0 || port > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between 0 and {ushort.MaxValue}");
        }
    }
}
