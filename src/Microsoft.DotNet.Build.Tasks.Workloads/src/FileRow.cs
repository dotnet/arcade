// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.WindowsInstaller;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Describes a single row of an MSI File table.
    /// </summary>
    public class FileRow
    {
        public int Attributes
        {
            get;
            set;
        }

        public string Component
        {
            get;
            set;
        }

        public string File
        {
            get;
            set;
        }

        public string FileName
        {
            get;
            set;
        }

        public int FileSize
        {
            get;
            set;
        }

        public string Language
        {
            get;
            set;
        }

        public string LongFileName
        {
            get
            {
                int index = FileName.IndexOf("|");

                if (index == -1)
                {
                    return FileName;
                }
                else
                {
                    return FileName.Substring(index + 1);
                }
            }
        }

        public int Sequence
        {
            get;
            set;
        }

        public string TargetPath
        {
            get;
            set;
        }

        public string Version
        {
            get;
            set;
        }

        public static FileRow Create(Record fileRecord, string targetPath)
        {
            return new FileRow
            {
                Attributes = (int)fileRecord["Attributes"],
                Component = (string)fileRecord["Component_"],
                File = (string)fileRecord["File"],
                FileName = (string)fileRecord["FileName"],
                FileSize = (int)fileRecord["FileSize"],
                Language = (string)fileRecord["Language"],
                Sequence = (int)fileRecord["Sequence"],
                TargetPath = targetPath,
                Version = (string)fileRecord["Version"],
            };
        }
    }
}
