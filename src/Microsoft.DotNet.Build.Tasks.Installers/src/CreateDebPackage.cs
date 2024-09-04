// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.Installers.src
{
    /// <summary>
    /// Create a .deb package from a control file and a data file.
    /// </summary>
    /// <remarks>
    /// Implements the format specified in https://manpages.debian.org/bookworm/dpkg-dev/deb.5.en.html
    /// </remarks>
    public sealed class CreateDebPackage : BuildTask
    {
        [Required]
        public string OutputDebPackagePath { get; set; }

        [Required]
        public ITaskItem ControlFile { get; set; }

        [Required]
        public ITaskItem DataFile { get; set; }

        public override bool Execute()
        {
            using ArWriter arWriter = new(File.OpenWrite(OutputDebPackagePath), false);
            arWriter.AddEntry(new ArEntry("debian-binary", 0, 0, 0, 0, new MemoryStream(Encoding.ASCII.GetBytes("2.0\n"))));
            using Stream controlFile = File.OpenRead(ControlFile.ItemSpec);
            arWriter.AddEntry(new ArEntry("control.tar.gz", 0, 0, 0, 0, controlFile));
            using Stream dataFile = File.OpenRead(DataFile.ItemSpec);
            arWriter.AddEntry(new ArEntry("data.tar.gz", 0, 0, 0, 0, dataFile));
            return true;
        }
    }
}
