// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.DotNet.TestsUtil
{
    public class TestHelpers
    {
        public static void DeleteDirectory(string path)
        {
            var retries = 10;
            while (retries > 0)
            {
                retries--;
                try
                {
                    Directory.Delete(path, recursive: true);
                    return;
                }
                catch (IOException ex)
                {
                    if (retries > 0)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    }
                    else
                    {
                        IList<string> lockedFiles;
                        try
                        {
                            lockedFiles = Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).ToList();
                        }
                        catch
                        {
                            // throw original exception if we can't figure out which files still exist
                            throw ex;
                        }

                        var sb = new StringBuilder("Failed to cleanup files:");
                        foreach (var file in lockedFiles)
                        {
                            sb.AppendLine(file);
                        }
                        throw new IOException(sb.ToString(), ex);
                    }
                }
            }
        }
    }
}
