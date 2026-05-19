// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SignCheck.Logging;
using Microsoft.SignCheck.Verification;

namespace SignCheckTask
{
    public class SignCheck
    {
        private static readonly char[] _wildcards = new char[] { '*', '?' };

        // Location where files can be downloaded
        private static readonly string _appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SignCheck");

        internal List<string> _inputFiles;

        internal Exclusions Exclusions
        {
            get;
            set;
        }

        internal bool LoggedResults
        {
            get;
            set;
        }

        internal string[] ResultDetails
        {
            get;
            set;
        }

        internal IEnumerable<string> InputFiles
        {
            get
            {
                if (_inputFiles == null)
                {
                    _inputFiles = GetInputFilesFromOptions();
                }
                return _inputFiles;
            }
        }

        public FileStatus FileStatus
        {
            get;
            set;
        }

        public bool HasArgErrors
        {
            get;
            set;
        }

        public Options Options
        {
            get;
            set;
        }

        public bool NoSignIssues
        {
            get;
            set;
        }

        public Log Log
        {
            get;
            set;
        }

        public int TotalFiles
        {
            get;
            set;
        }

        public int TotalUnsignedFiles
        {
            get;
            set;
        }

        public int TotalSignedFiles
        {
            get;
            set;
        }

        public int TotalSkippedFiles
        {
            get;
            set;
        }

        public int TotalDoNotUnpackFiles
        {
            get;
            set;
        }

        public int TotalExcludedFiles
        {
            get;
            set;
        }

        public int TotalSkippedExcludedFiles
        {
            get;
            set;
        }

        public SignCheck(string[] args)
        {
            Options = new Options();
            RootCommand rootCommand = BuildRootCommand(parsedOptions => HandleOptions(parsedOptions));
            ParseResult parseResult = rootCommand.Parse(args);

            if (parseResult.Errors.Count > 0)
            {
                foreach (var error in parseResult.Errors)
                {
                    Console.Error.WriteLine(error.Message);
                }
                HasArgErrors = true;
                return;
            }

            // Invoke executes the registered action (which calls HandleOptions) or prints help.
            // When --help/--version is requested, no action runs and Options is left in its initial state;
            // signal that to callers so they don't attempt to run.
            int invokeResult = parseResult.Invoke();
            if (Log == null)
            {
                HasArgErrors = true;
            }
        }

        public SignCheck(Options options)
        {
            HandleOptions(options ?? new Options());
        }

        private static RootCommand BuildRootCommand(Action<Options> onParsed)
        {
            Option<string> errorLogFileOption = new("--error-log-file", "-e")
            {
                Description = "Log errors to a separate file. If the file already exists it will be overwritten."
            };
            Option<string[]> fileStatusOption = new("--file-status", "-f")
            {
                Description = "Report the status of a specific set of files. Any combination of the following values are allowed. Values are separated by a ','. 'UnsignedFiles', 'SignedFiles', 'SkippedFiles', 'ExcludedFiles', 'AllFiles'. Default is 'UnsignedFiles'",
                CustomParser = result => result.Tokens.SelectMany(t => t.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)).ToArray(),
                AllowMultipleArgumentsPerToken = true
            };
            Option<string> exclusionsOutputOption = new("--generate-exclusions-file", "-g")
            {
                Description = "Name of the exclusions file to generate. The entries in the file are generated using reported unsigned files. If the file already exists it will be overwritten."
            };
            Option<string[]> inputFilesOption = new("--input-files", "-i")
            {
                Description = "A list of files to scan. Wildcards (* and ?) are supported. You can specify groups of files, e.g. C:\\Dir1\\Dir*\\File?.EXE or a URL (http or https).",
                AllowMultipleArgumentsPerToken = true
            };
            Option<bool> enableJarOption = new("--verify-jar", "-j")
            {
                Description = "Enable JAR signature verification. By default, .jar files are not verified."
            };
            Option<string> logFileOption = new("--log-file", "-l")
            {
                Description = "Output results to the specified log file. If the file already exists it will be overwritten."
            };
            Option<string> resultsXmlFileOption = new("--results-xml-file")
            {
                Description = "Output signing results to the specified XML log file. If the file already exists it will be overwritten."
            };
            Option<bool> enableXmlOption = new("--verify-xml", "-m")
            {
                Description = "Enable XML signature verification. By default, .xml files are not verified."
            };
            Option<bool> skipTimestampOption = new("--skip-timestamp", "-p")
            {
                Description = "Ignore timestamp checks for AuthentiCode signatures."
            };
            Option<bool> recursiveOption = new("--recursive", "-r")
            {
                Description = "Traverse subdirectories or container files such as .zip, .nupkg, .cab, and .msi"
            };
            Option<bool> verifyStrongNameOption = new("--verify-strongname", "-s")
            {
                Description = "Enable strongname checks for managed code files (.exe and .dll)"
            };
            Option<bool> traverseSubFoldersOption = new("--traverse-subfolders", "-t")
            {
                Description = "Traverse subfolders to find files matching wildcard patterns used by the --input-files option."
            };
            Option<LogVerbosity> verbosityOption = new("--verbosity", "-v")
            {
                Description = "Set the verbosity level: Minimum, Normal, Detailed, Diagnostic.",
                DefaultValueFactory = _ => LogVerbosity.Normal
            };
            Option<string> exclusionsFileOption = new("--exclusions-file", "-x")
            {
                Description = "Path to a file containing a list of files to ignore when verification fails. Exclusions are not reported as errors."
            };

            RootCommand rootCommand = new("SignCheck - Build artifact signing validation tool")
            {
                errorLogFileOption,
                fileStatusOption,
                exclusionsOutputOption,
                inputFilesOption,
                enableJarOption,
                logFileOption,
                resultsXmlFileOption,
                enableXmlOption,
                skipTimestampOption,
                recursiveOption,
                verifyStrongNameOption,
                traverseSubFoldersOption,
                verbosityOption,
                exclusionsFileOption,
            };

            rootCommand.SetAction(result =>
            {
                Options options = new()
                {
                    ErrorLogFile = result.GetValue(errorLogFileOption),
                    FileStatus = result.GetValue(fileStatusOption) ?? Array.Empty<string>(),
                    ExclusionsOutput = result.GetValue(exclusionsOutputOption),
                    InputFiles = result.GetValue(inputFilesOption) ?? Array.Empty<string>(),
                    EnableJarSignatureVerification = result.GetValue(enableJarOption),
                    LogFile = result.GetValue(logFileOption),
                    ResultsXmlFile = result.GetValue(resultsXmlFileOption),
                    EnableXmlSignatureVerification = result.GetValue(enableXmlOption),
                    SkipTimestamp = result.GetValue(skipTimestampOption),
                    Recursive = result.GetValue(recursiveOption),
                    VerifyStrongName = result.GetValue(verifyStrongNameOption),
                    TraverseSubFolders = result.GetValue(traverseSubFoldersOption),
                    Verbosity = result.GetValue(verbosityOption),
                    ExclusionsFile = result.GetValue(exclusionsFileOption),
                };
                onParsed(options);
                return 0;
            });

            return rootCommand;
        }

        private void HandleOptions(Options options)
        {
            Options = options;

            Log = new Log(options.LogFile, options.ErrorLogFile, options.ResultsXmlFile, options.Verbosity);

            if (Options.FileStatus.Count() > 0)
            {
                FileStatus = FileStatus.NoFiles;
                foreach (string value in Options.FileStatus)
                {
                    FileStatus result;
                    if (Enum.TryParse<FileStatus>(value, out result))
                    {
                        FileStatus |= result;
                    }
                    else
                    {
                        Log.WriteError(LogVerbosity.Minimum, SignCheckResources.scErrorUnknownFileStatus, value);
                    }
                }

                if (FileStatus == FileStatus.NoFiles)
                {
                }
            }
            else
            {
                FileStatus = FileStatus.UnsignedFiles;
            }

            if (!String.IsNullOrEmpty(Options.ExclusionsFile))
            {
                ProcessExclusions(Options.ExclusionsFile);
            }
            else
            {
                Exclusions = new Exclusions();
            }
            // Add some well-known exclusions for WiX
            Exclusions.Add(new Exclusion("*netfxca*;*.msi;Wix custom action (NGEN"));
            Exclusions.Add(new Exclusion("*wixdepca*;*.msi;WiX custom action"));
            Exclusions.Add(new Exclusion("*wixuiwixca*;*.msi;WiX custom action"));
            Exclusions.Add(new Exclusion("*wixca*;*.msi;Wix custom action"));
            Exclusions.Add(new Exclusion("*wixstdba.dll*;*.exe;WiX standard bundle application"));

            if (!Directory.Exists(_appData))
            {
                Directory.CreateDirectory(_appData);
            }
        }

        private List<string> GetInputFilesFromOptions()
        {
            var inputFiles = new List<string>();
            var downloadFiles = new List<Uri>();
            if (Options.InputFiles == null)
            {
                return inputFiles;
            }
            foreach (string inputFile in Options.InputFiles)
            {
                Uri uriResult;

                if ((Uri.TryCreate(inputFile, UriKind.Absolute, out uriResult)) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    string downloadPath = Path.Combine(_appData, Path.GetFileName(uriResult.LocalPath));
                    inputFiles.Add(downloadPath);
                    downloadFiles.Add(uriResult);
                }
                else if (inputFile.IndexOfAny(_wildcards) > -1)
                {
                    SearchOption fileSearchOptions = Options.TraverseSubFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    string fileSearchPath = Path.GetDirectoryName(inputFile);
                    string fileSearchPattern = Path.GetFileName(inputFile);
                    string[] matchedFiles = null;

                    if (String.IsNullOrEmpty(fileSearchPath))
                    {
                        // CASE 1: No path, pattern in filename, e.g. "--input-File *.txt" or "-i Foo?Bar.txt"
                        fileSearchPath = Directory.GetCurrentDirectory();
                        matchedFiles = Directory.GetFiles(fileSearchPath, fileSearchPattern, fileSearchOptions);
                    }
                    else
                    {
                        if (fileSearchPath.IndexOfAny(_wildcards) > -1)
                        {
                            // CASE 2: Path contains wildcards, e.g. "-i C:\Foo*\Bar.txt" or "-i C:\Foo*\Bar*.txt"
                            string[] wildcardDirectories = Utils.GetDirectories(fileSearchPath, null, fileSearchOptions);

                            var _matchedFiles = new List<string>();

                            foreach (string dir in wildcardDirectories)
                            {
                                _matchedFiles.AddRange(Directory.GetFiles(dir, fileSearchPattern, fileSearchOptions));
                            }

                            matchedFiles = _matchedFiles.ToArray();
                        }
                        else
                        {
                            // CASE 3: Path contains no search patterns, e.g. "-i C:\Foo\Bar\*.txt"
                            if (Directory.Exists(fileSearchPath))
                            {
                                matchedFiles = Directory.GetFiles(fileSearchPath, fileSearchPattern, fileSearchOptions);
                            }
                            else
                            {
                                Log.WriteError(String.Format(SignCheckResources.scDirDoesNotExist, fileSearchPath));
                            }
                        }
                    }

                    if (matchedFiles != null)
                    {
                        foreach (string file in matchedFiles)
                        {
                            inputFiles.Add(file);
                        }
                    }
                }
                else
                {
                    if (Directory.Exists(inputFile))
                    {
                        SearchOption searchOption = Options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                        foreach (string dirFile in Directory.GetFiles(inputFile, "*.*", searchOption))
                        {
                            inputFiles.Add(dirFile);
                        }
                    }
                    else if (File.Exists(Path.GetFullPath(inputFile)))
                    {
                        inputFiles.Add(inputFile);
                    }
                    else
                    {
                        Log.WriteError(String.Format(SignCheckResources.scInputFileDoesNotExist, inputFile));
                    }
                }
            }

            if (downloadFiles.Count > 0)
            {
                DownloadFilesAsync(downloadFiles).Wait();
            }

            // Exclude log files in case they are created in the folder being scanned.
            if (!String.IsNullOrEmpty(Options.ErrorLogFile))
            {
                inputFiles.Remove(Path.GetFullPath(Options.ErrorLogFile));
            }

            if (!String.IsNullOrEmpty(Options.LogFile))
            {
                inputFiles.Remove(Path.GetFullPath(Options.LogFile));
            }
            return inputFiles;
        }

        private void ProcessExclusions(string exclusionsFile)
        {
            Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.scProcessExclusions, exclusionsFile);
            Exclusions = new Exclusions(exclusionsFile);
        }

        private void ProcessResults(IEnumerable<SignatureVerificationResult> results, int indent)
        {
            foreach (SignatureVerificationResult result in results)
            {
                TotalFiles++;
                string outcome = "Unknown";

                if (result.IsSigned && !result.IsExcluded)
                {
                    TotalSignedFiles++;
                    outcome = "Signed";
                }
                else if (!(result.IsExcluded || result.IsSkipped) && (!result.IsSigned && !result.IsDoNotSign))
                {
                    TotalUnsignedFiles++;
                    outcome = "Unsigned";
                }

                if (result.IsExcluded || (!result.IsSigned && result.IsDoNotSign))
                {
                    TotalExcludedFiles++;
                    outcome = "Excluded";
                }

                if (result.IsSkipped)
                {
                    TotalSkippedFiles++;
                    outcome = "Skipped";
                }

                if (result.IsSkipped && result.IsExcluded)
                {
                    TotalSkippedExcludedFiles++;
                    outcome = "SkippedExcluded";
                }

                if (result.IsDoNotUnpack)
                {
                    TotalDoNotUnpackFiles++;
                }

                // Regardless of the file status reporting settings, a container file like an MSI or NuGet package
                // is always reported to keep the file hierarchy in the log readable.
                if (((result.IsSkipped) && ((FileStatus & FileStatus.SkippedFiles) != 0)) ||
                    ((result.IsSigned) && ((FileStatus & FileStatus.SignedFiles) != 0)) ||
                    ((result.IsExcluded) && ((FileStatus & FileStatus.ExcludedFiles) != 0)) ||
                    ((result.IsSigned) && (result.IsDoNotSign)) ||
                    ((result.NestedResults.Count() > 0) && (Options.Recursive)) ||
                    ((FileStatus & FileStatus.AllFiles) == FileStatus.AllFiles) ||
                    ((!result.IsSigned && !result.IsDoNotSign) && (!result.IsSkipped) && (!result.IsExcluded) && ((FileStatus & FileStatus.UnsignedFiles) != 0)))
                {
                    LoggedResults = true;
                    Log.WriteMessage(LogVerbosity.Minimum, String.Empty.PadLeft(indent) + result.ToString(result.IsExcluded ? DetailKeys.ResultKeysExcluded : ResultDetails));
                }

                if (((!result.IsSigned) && (!(result.IsSkipped || result.IsExcluded || result.IsDoNotSign))) || (result.IsSigned && result.IsDoNotSign))
                {
                    NoSignIssues = false;
                }

                Log.WriteStartResult(result, outcome);
                if (result.NestedResults.Count > 0)
                {
                    ProcessResults(result.NestedResults, indent + 2);
                }
                Log.WriteEndResult();
            }
        }

        public void GenerateExclusionsFile(StreamWriter writer, IEnumerable<SignatureVerificationResult> results)
        {
            foreach (SignatureVerificationResult result in results)
            {
                if ((!result.IsSigned) && (!result.IsSkipped))
                {
                    writer.WriteLine(result.ExclusionEntry);
                }

                if (result.NestedResults.Count > 0)
                {
                    GenerateExclusionsFile(writer, result.NestedResults);
                }
            }
        }

        public int Run()
        {
            try
            {
                Log.WriteMessage("Starting execution of SignCheck.");

                SignatureVerificationOptions options = SignatureVerificationOptions.None;
                options |= Options.Recursive ? SignatureVerificationOptions.VerifyRecursive : SignatureVerificationOptions.None;
                options |= Options.EnableXmlSignatureVerification ? SignatureVerificationOptions.VerifyXmlSignatures : SignatureVerificationOptions.None;
                options |= Options.SkipTimestamp ? SignatureVerificationOptions.None : SignatureVerificationOptions.VerifyAuthentiCodeTimestamps;
                options |= Options.VerifyStrongName ? SignatureVerificationOptions.VerifyStrongNameSignature : SignatureVerificationOptions.None;
                options |= Options.EnableJarSignatureVerification ? SignatureVerificationOptions.VerifyJarSignatures : SignatureVerificationOptions.None;
                options |= !String.IsNullOrEmpty(Options.ExclusionsOutput) ? SignatureVerificationOptions.GenerateExclusion : SignatureVerificationOptions.None;

                var signatureVerificationManager = new SignatureVerificationManager(Exclusions, Log, options);

                ResultDetails = Options.Verbosity > LogVerbosity.Normal ? DetailKeys.ResultKeysVerbose : DetailKeys.ResultKeysNormal;

                if (InputFiles != null && InputFiles.Count() > 0)
                {
                    DateTime startTime = DateTime.Now;
                    IEnumerable<SignatureVerificationResult> results = signatureVerificationManager.VerifyFiles(InputFiles);
                    DateTime endTime = DateTime.Now;

                    NoSignIssues = true;
                    Log.WriteLine();
                    Log.WriteMessage(LogVerbosity.Minimum, SignCheckResources.scResults);
                    Log.WriteLine();
                    ProcessResults(results, 0);

                    // Generate an exclusions file for any unsigned files that were reported.
                    if (!String.IsNullOrEmpty(Options.ExclusionsOutput))
                    {
                        if (!Directory.Exists(Options.ExclusionsOutput))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Options.ExclusionsOutput)));
                        }
                        using (var exclusionsWriter = new StreamWriter(Options.ExclusionsOutput, append: false))
                        {
                            GenerateExclusionsFile(exclusionsWriter, results);
                        }
                    }

                    if (LoggedResults)
                    {
                        Log.WriteLine();
                    }

                    if (NoSignIssues)
                    {
                        Log.WriteMessage(LogVerbosity.Minimum, SignCheckResources.scNoSignIssues);
                    }
                    else
                    {
                        Log.WriteError(LogVerbosity.Minimum, SignCheckResources.scSignIssuesFound);
                    }

                    TimeSpan totalTime = endTime - startTime;
                    Log.WriteMessage(LogVerbosity.Minimum, String.Format(SignCheckResources.scTime, totalTime));
                    Log.WriteMessage(LogVerbosity.Minimum, String.Format(SignCheckResources.scStats,
                        TotalFiles, TotalSignedFiles, TotalUnsignedFiles, TotalSkippedFiles, TotalExcludedFiles, TotalSkippedExcludedFiles, TotalDoNotUnpackFiles));
                }
                else
                {
                    Log.WriteMessage(LogVerbosity.Minimum, SignCheckResources.scNoFilesProcessed);
                }
            }

            catch (Exception e)
            {
                Log.WriteError(e.ToString());
            }
            finally
            {
                if (Log != null)
                {
                    Log.Close();
                }
            }

            return Log.HasLoggedErrors ? -1 : 0;
        }

        private async Task DownloadFileAsync(Uri uri)
        {
            try
            {
                using (var httpClient = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true }))
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10000); // 10 seconds

                    string downloadPath = Path.Combine(_appData, Path.GetFileName(uri.LocalPath));

                    if (File.Exists(downloadPath))
                    {
                        Log.WriteMessage(LogVerbosity.Detailed, SignCheckResources.scDeleteExistingFile, downloadPath);
                        File.Delete(downloadPath);
                    }

                    Log.WriteMessage(LogVerbosity.Detailed, SignCheckResources.scDownloading, uri.AbsoluteUri, downloadPath);

                    using (var stream = await httpClient.GetStreamAsync(uri))
                    {
                        using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteError(e.Message);
            }
        }

        private async Task DownloadFilesAsync(IEnumerable<Uri> uris)
        {
            await Task.WhenAll(uris.Select(u => DownloadFileAsync(u)));
        }
    }
}
