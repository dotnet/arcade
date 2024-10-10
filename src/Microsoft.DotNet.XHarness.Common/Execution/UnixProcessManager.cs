// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.Common.Execution;

public abstract class UnixProcessManager : ProcessManager
{
    protected override List<int> GetChildProcessIds(ILog log, int pid) => GetChildProcessIdsInternal(log, pid);

    protected static List<int> GetChildProcessIdsInternal(ILog log, int pid)
    {
        var list = new List<int>();

        using (var ps = new Process())
        {
            ps.StartInfo.FileName = "ps";
            ps.StartInfo.Arguments = "-eo ppid,pid";
            ps.StartInfo.UseShellExecute = false;
            ps.StartInfo.RedirectStandardOutput = true;
            ps.Start();

            string stdout = ps.StandardOutput.ReadToEnd();

            if (!ps.WaitForExit(1000))
            {
                log.WriteLine("ps didn't finish in a reasonable amount of time (1 second).");
                return list;
            }

            if (ps.ExitCode != 0)
            {
                return list;
            }

            stdout = stdout.Trim();

            if (string.IsNullOrEmpty(stdout))
            {
                return list;
            }

            var dict = new Dictionary<int, List<int>>();
            foreach (string line in stdout.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var l = line.Trim();
                var space = l.IndexOf(' ');
                if (space <= 0)
                {
                    continue;
                }

                var parent = l.Substring(0, space);
                var process = l.Substring(space + 1);

                if (int.TryParse(parent, out var parent_id) && int.TryParse(process, out var process_id))
                {
                    if (!dict.TryGetValue(parent_id, out var children))
                    {
                        dict[parent_id] = children = new List<int>();
                    }

                    children.Add(process_id);
                }
            }

            var queue = new Queue<int>();
            queue.Enqueue(pid);

            do
            {
                var parent_id = queue.Dequeue();
                list.Add(parent_id);
                if (dict.TryGetValue(parent_id, out var children))
                {
                    foreach (var child in children)
                    {
                        queue.Enqueue(child);
                    }
                }
            } while (queue.Count > 0);
        }

        return list;
    }
}
