// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Listeners;

public class SimpleHttpListener : SimpleListener
{
    private readonly bool _autoExit;
    private HttpListener _server;
    private bool _connected_once;

    public int Port { get; private set; }

    public SimpleHttpListener(ILog log, IFileBackedLog testLog, bool autoExit) : base(log, testLog)
    {
        _autoExit = autoExit;
    }

    public override int InitializeAndGetPort()
    {
        _server = new HttpListener();

        if (Port != 0)
        {
            throw new NotImplementedException();
        }

        // Try and find an unused port
        int attemptsLeft = 50;
        var r = new Random((int)DateTime.Now.Ticks);
        while (attemptsLeft-- > 0)
        {
            var newPort = r.Next(49152, 65535); // The suggested range for dynamic ports is 49152-65535 (IANA)
            _server.Prefixes.Clear();
            _server.Prefixes.Add("http://*:" + newPort + "/");
            try
            {
                _server.Start();
                Port = newPort;
                break;
            }
            catch (Exception ex)
            {
                Log.WriteLine("Failed to listen on port {0}: {1}", newPort, ex.Message);
            }
        }

        return Port;
    }

    protected override void Stop() => _server.Stop();

    protected override void Start()
    {
        bool processed;

        try
        {
            Log.WriteLine("Test log server listening on: {0}:{1}", Address, Port);
            do
            {
                var context = _server.GetContext();
                processed = Processing(context);
            } while (!_autoExit || !processed);
        }
        catch (Exception e)
        {
            if (e is not SocketException se || se.SocketErrorCode != SocketError.Interrupted)
            {
                Console.WriteLine("[{0}] : {1}", DateTime.Now, e);
            }
        }
        finally
        {
            try
            {
                _server.Stop();
            }
            finally
            {
                Finished();
            }
        }
    }

    private bool Processing(HttpListenerContext context)
    {
        var finished = false;

        var request = context.Request;
        var response = "OK";

        var stream = request.InputStream;
        var data = string.Empty;
        using (var reader = new StreamReader(stream))
        {
            data = reader.ReadToEnd();
        }

        stream.Close();

        switch (request.RawUrl)
        {
            case "/Start":
                if (!_connected_once)
                {
                    _connected_once = true;
                    Connected(request.RemoteEndPoint.ToString());
                }
                break;
            case "/Finish":
                if (!finished)
                {
                    TestLog.Write(data);
                    TestLog.Flush();
                    finished = true;
                }
                break;
            default:
                Log.WriteLine("Unknown upload url: {0}", request.RawUrl);
                response = $"Unknown upload url: {request.RawUrl}"; // CodeQL [SM02175] False Positive: This is a plain-text API response
                break;
        }

        var buf = System.Text.Encoding.UTF8.GetBytes(response);
        context.Response.ContentLength64 = buf.Length;
        context.Response.OutputStream.Write(buf, 0, buf.Length);
        context.Response.OutputStream.Close();
        context.Response.Close();

        return finished;
    }
}

