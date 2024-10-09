// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Listeners;

public class SimpleFileListener : SimpleListener
{
    private readonly bool _xmlOutput;
    private Thread _processorThread;
    private bool _cancel;

    public string Path { get; private set; }

    public SimpleFileListener(string path, ILog log, IFileBackedLog testLog, bool xmlOutput) : base(log, testLog)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        _xmlOutput = xmlOutput;
    }

    protected override void Stop()
    {
        _cancel = true;
        _processorThread.Join();
        _processorThread = null;
        Finished(true);
    }

    public override int InitializeAndGetPort()
    {
        _processorThread = new Thread(Processing);
        return 0;
    }

    protected override void Start() => _processorThread.Start();

    private void Processing()
    {
        Connected("N/A");
        using (var fs = new BlockingFileStream(Path, this))
        {
            using (var reader = new StreamReader(fs))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    TestLog.WriteLine(line);
                    if (line.StartsWith("[Runner executing:", StringComparison.Ordinal))
                    {
                        Log.WriteLine("Tests have started executing");
                    }
                    else if (!_xmlOutput && line.StartsWith("Tests run: ", StringComparison.Ordinal))
                    {
                        Log.WriteLine("Tests have finished executing");
                        break;
                    }
                    else if (_xmlOutput && line == "<!-- the end -->")
                    {
                        Log.WriteLine("Tests have finished executing");
                        break;
                    }
                }
            }
        }
        TestLog.Flush();
        Finished();
    }

    private class BlockingFileStream : FileStream
    {
        private readonly SimpleFileListener _listener;
        private long _lastPosition;

        public BlockingFileStream(string path, SimpleFileListener listener)
            : base(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite)
        {
            _listener = listener;
        }

        public override int Read(byte[] array, int offset, int count)
        {
            while (_lastPosition == base.Length && !_listener._cancel)
            {
                Thread.Sleep(25);
            }

            var rv = base.Read(array, offset, count);
            _lastPosition += rv;
            return rv;
        }
    }
}

