using CommandLine;
using Microsoft.SignCheck.Logging;
using System.Collections.Generic;

namespace SignCheck
{
    public class Options
    {
        [Option('e', "error-log-file",
            HelpText = "Log errors to a separate file. If the file already exists it will be overwritten.")]
        public string ErrorLogFile
        {
            get;
            set;
        }

        [Option('f', "file-status",
            Separator = ',',
            HelpText = "Report the status of a speficic set of files. Any combination of the following values are allowed. Values are separated by a ','. 'UnsignedFiles', 'SignedFiles', 'SkippedFiles', 'ExcludedFiles', 'AllFiles'. Default is 'UnsignedFiles'")]
        public IEnumerable<string> FileStatus
        {
            get;
            set;
        }

        [Option('g', "generate-exclusions-file",
            HelpText = "Name of the exclusions file to generate. The entries in the file are generated using reported unsigned files. If the file already exists it will be overwritten.")]
        public string ExclusionsOutput
        {
            get;
            set;
        }

        [Option('i', "input-files",
            HelpText = "A list of files to scan. Wildcards (* and ?) are supported. For example, you can specify a group of files as C:\\Dir1\\Dir*\\File?.EXE")]
        public IEnumerable<string> InputFiles
        {
            get;
            set;
        }

        [Option('l', "log-file",
            HelpText = "Output results to the specified log file. If the file already exists it will be overwritten.")]
        public string LogFile
        {
            get;
            set;
        }

        [Option('p', "skip-timestamp",
            HelpText = "Ignore timestamp checks for AuthentiCode signatures.")]
        public bool SkipTimestamp
        {
            get;
            set;
        }

        [Option('r', "recursive",
            HelpText = "Traverse subdirectories or container files such as .zip, .nupkg, .cab, and .msi")]
        public bool Recursive
        {
            get;
            set;
        }        

        [Option('s', "skip-strongname",
            HelpText = "Skip strongname checks for managed code files (.exe and .dll)")]
        public bool SkipStrongname
        {
            get;
            set;
        }

        [Option('t', "traverse-subfolders",
            HelpText = "Traverse subfolders to find files matching wildcard patterns used by the --input-files option.")]
        public bool TraverseSubFolders
        {
            get;
            set;
        }

        [Option('v', "verbosity",
            HelpText = "Set the verbosity level: Minimum, Normal, Detailed, Diagnostic")]
        public LogVerbosity Verbosity
        {
            get;
            set;
        }

        [Option('x', "exclusions-file",
            HelpText = "Path to a file containing a list of files to ignore when verification fails. Exclusions are not reported as errors")]
        public string ExclusionsFile
        {
            get;
            set;
        }

    }
}
