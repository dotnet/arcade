// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SignCheck.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.DotNet.SignCheck.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67" +
                                                                          "871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0b" +
                                                                          "d333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307" +
                                                                          "e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c3" +
                                                                          "08055da9")]

namespace Microsoft.DotNet.SignCheck
{
    public class SignCheckTask : MSBuildTaskBase
    {
        public bool EnableJarSignatureVerification
        {
            get;
            set;
        }
        public bool EnableXmlSignatureVerification
        {
            get;
            set;
        }
        public string FileStatus
        {
            get;
            set;
        }

        public string ExclusionsOutput
        {
            get;
            set;
        }

        public bool SkipTimestamp
        {
            get;
            set;
        }
        public bool Recursive
        {
            get;
            set;
        }
        
        public bool VerifyStrongName
        {
            get;
            set;
        }
        public string ExclusionsFile
        {
            get;
            set;
        }
        public ITaskItem[] InputFiles
        {
            get;
            set;
        }
        [Required]
        public string LogFile
        {
            get;
            set;
        }
        [Required]
        public string ErrorLogFile
        {
            get;
            set;
        }
        public string Verbosity
        {
            get;
            set;
        }
        public string ArtifactFolder
        {
            get;
            set;
        }

        private SignCheck _signCheck;

        public bool ExecuteTask()
        {
            Options options = new Options();
            options.EnableJarSignatureVerification = EnableJarSignatureVerification;
            options.EnableXmlSignatureVerification = EnableXmlSignatureVerification;
            options.ExclusionsFile = ExclusionsFile;
            options.ExclusionsOutput = ExclusionsOutput;
            string[] filestatuses = FileStatus.Split(';', ',');
            options.FileStatus = filestatuses;
            options.Recursive = Recursive;
            options.TraverseSubFolders = Recursive;
            options.SkipTimestamp = SkipTimestamp;
            options.VerifyStrongName = VerifyStrongName;
            options.LogFile = LogFile;
            options.ErrorLogFile = ErrorLogFile;

            List<string> inputFiles = new List<string>();
            if (InputFiles != null)
            {
                ArtifactFolder = ArtifactFolder ?? Environment.CurrentDirectory;
                SearchOption fileSearchOptions = Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                foreach (var checkFile in InputFiles.Select(s => s.ItemSpec).ToArray())
                {
                    if (Path.IsPathRooted(checkFile))
                    {
                        inputFiles.Add(checkFile);
                    }
                    else
                    {
                        var matchedFiles = Directory.GetFiles(ArtifactFolder, checkFile, fileSearchOptions);

                        if(matchedFiles.Length == 1)
                        {
                            inputFiles.Add(matchedFiles[0]);
                        }
                        else if(matchedFiles.Length == 0)
                        {
                            Log.LogError($"Unable to find file '{checkFile}' in folder '{ArtifactFolder}'.  Try specifying 'Recursive=true` to include subfolders");
                        }
                        else if (matchedFiles.Length > 1)
                        {
                            Log.LogError($"found multiple files matching pattern '{checkFile}'");
                            foreach(var file in matchedFiles)
                            {
                                Log.LogError($" - {file}");
                            }
                        }
                    }
                }
            }
            options.InputFiles = inputFiles.ToArray();

            if(Enum.TryParse<LogVerbosity>(Verbosity, out LogVerbosity verbosity))
            {
                options.Verbosity = verbosity;
            }

            _signCheck = new SignCheck(options);
            int result = _signCheck.Run();
            return (result == 0 && !Log.HasLoggedErrors);
        }
        public override void ConfigureServices(IServiceCollection collection)
        {
        }
    }
}
