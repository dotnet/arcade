using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.SignCheck;
using Microsoft.SignCheck.Logging;
using Microsoft.SignCheck.Verification;

namespace SignCheck
{
    internal class SignCheck
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

        internal bool HasArgErrors
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

        public Options Options
        {
            get;
            set;
        }

        public bool AllFilesSigned
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

        public int TotalExcludedFiles
        {
            get;
            set;
        }

        public SignCheck(string[] args)
        {
            Options = new Options();
            var parseResult = Parser.Default.ParseArguments<Options>(args).
                WithParsed(options => HandleOptions(options)).
                WithNotParsed<Options>(errors => HandleErrors(errors));
        }

        private void HandleOptions(Options options)
        {
            Options = options;

            Log = new Log(options.LogFile, options.ErrorLogFile, options.Verbosity);

            if (Options.FileStatus.Count() > 0)
            {
                FileStatus = FileStatus.NoFiles;
                foreach (var v in Options.FileStatus)
                {
                    FileStatus result;
                    if (Enum.TryParse<FileStatus>(v, out result))
                    {
                        FileStatus |= result;
                    }
                    else
                    {
                        Log.WriteError(LogVerbosity.Minimum, SignCheckResources.scErrorUnknownFileStatus, v);
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

            Log.WriteMessage(LogVerbosity.Diagnostic, String.Format(SignCheckResources.scDetailFileStatusSetting, FileStatus.ToString()));

            if (!String.IsNullOrEmpty(Options.ExclusionsFile))
            {
                ProcessExclusions(Options.ExclusionsFile);
            }
            else
            {
                Exclusions = new Exclusions();
            }

            if (!Directory.Exists(_appData))
            {
                Directory.CreateDirectory(_appData);
            }
        }

        private void HandleErrors(IEnumerable<Error> errors)
        {
            HasArgErrors = true;
        }

        private List<string> GetInputFilesFromOptions()
        {
            var inputFiles = new List<string>();
            var downloadFiles = new List<Uri>();

            foreach (var inputFile in Options.InputFiles)
            {
                Uri uriResult;

                if ((Uri.TryCreate(inputFile, UriKind.Absolute, out uriResult)) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    var downloadPath = Path.Combine(_appData, Path.GetFileName(uriResult.LocalPath));
                    inputFiles.Add(downloadPath);
                    downloadFiles.Add(uriResult);
                }
                else if (inputFile.IndexOfAny(_wildcards) > -1)
                {
                    var fileSearchOptions = Options.TraverseSubFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var fileSearchPath = Path.GetDirectoryName(inputFile);
                    var fileSearchPattern = Path.GetFileName(inputFile);
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
                            var wildcardDirectories = Utils.GetDirectories(fileSearchPath, null, fileSearchOptions);

                            var _matchedFiles = new List<string>();

                            foreach (var dir in wildcardDirectories)
                            {
                                _matchedFiles.AddRange(Directory.GetFiles(dir, fileSearchPattern, fileSearchOptions));
                            }

                            matchedFiles = _matchedFiles.ToArray();
                        }
                        else
                        {
                            // CASE 3: Path contains no search patterns, e.g. "-i C:\Foo\Bar\*.txt"
                            matchedFiles = Directory.GetFiles(fileSearchPath, fileSearchPattern, fileSearchOptions);
                        }
                    }

                    foreach (var file in matchedFiles)
                    {
                        inputFiles.Add(file);
                    }
                }
                else
                {
                    if (Directory.Exists(inputFile))
                    {
                        var searchOption = Options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                        foreach (var dirFile in Directory.GetFiles(inputFile, "*.*", searchOption))
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

            return inputFiles;
        }

        private void ProcessExclusions(string exclusionsFile)
        {
            Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.scProcessExclusions);
            Exclusions = new Exclusions(exclusionsFile);
        }

        private void ProcessResults(IEnumerable<SignatureVerificationResult> results, int indent)
        {
            foreach (var result in results)
            {
                TotalFiles++;

                if (result.IsExcluded)
                {
                    TotalExcludedFiles++;
                }

                if (result.IsSigned)
                {
                    TotalSignedFiles++;
                }
                else if (!(result.IsExcluded || result.IsSkipped))
                {
                    TotalUnsignedFiles++;
                }

                if (result.IsSkipped)
                {
                    TotalSkippedFiles++;
                }

                // Regardless of the file status reporting settings, a container file like an MSI or NuGet package
                // is always reported to keep the file hierarchy in the log readable.
                if (((result.IsSkipped) && ((FileStatus & FileStatus.SkippedFiles) != 0)) ||
                    ((result.IsSigned) && ((FileStatus & FileStatus.SignedFiles) != 0)) ||
                    ((result.IsExcluded) && ((FileStatus & FileStatus.ExcludedFiles) != 0)) ||
                    ((result.NestedResults.Count() > 0) && (Options.Recursive)) ||
                    ((FileStatus & FileStatus.AllFiles) == FileStatus.AllFiles) ||
                    ((!result.IsSigned) && (!result.IsSkipped) && ((FileStatus & FileStatus.UnsignedFiles) != 0)))
                {
                    Log.WriteMessage(LogVerbosity.Minimum, String.Empty.PadLeft(indent) + result.ToString(ResultDetails));
                }

                if ((!result.IsSigned) && (!(result.IsSkipped || result.IsExcluded)))
                {
                    AllFilesSigned = false;
                }

                if (result.NestedResults.Count > 0)
                {
                    ProcessResults(result.NestedResults, indent + 2);
                }
            }
        }

        public void GenerateExclusionsFile(StreamWriter writer, IEnumerable<SignatureVerificationResult> results)
        {
            foreach (var result in results)
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

        private int Run()
        {
            try
            {
                var signatureVerifier = new SignatureVerifier(Options.Verbosity, Exclusions, Log)
                {
                    EnableXmlSignatureVerification = Options.EnableXmlSignatureVerification,
                    Recursive = Options.Recursive,
                    SkipAuthentiCodeTimestamp = Options.SkipTimestamp,
                    VerifyStrongName = Options.VerifyStrongName
                };

                ResultDetails = Options.Verbosity > LogVerbosity.Normal ? DetailKeys.ResultKeysVerbose : DetailKeys.ResultKeysNormal;

                if (InputFiles.Count() > 0)
                {
                    var results = signatureVerifier.Verify(InputFiles);

                    AllFilesSigned = true;
                    Log.WriteLine();
                    Log.WriteMessage(LogVerbosity.Minimum, SignCheckResources.scResults);
                    Log.WriteLine();
                    ProcessResults(results, 0);

                    // Generate an exclusions file for any unsigned files that were reported.
                    if (!String.IsNullOrEmpty(Options.ExclusionsOutput))
                    {
                        using (var exclusionsWriter = new StreamWriter(Options.ExclusionsOutput, append: false))
                        {
                            GenerateExclusionsFile(exclusionsWriter, results);
                        }
                    }

                    Log.WriteLine();
                    if (AllFilesSigned)
                    {
                        Log.WriteMessage(LogVerbosity.Minimum, SignCheckResources.scAllFilesSigned);
                    }
                    else
                    {
                        Log.WriteError(LogVerbosity.Minimum, SignCheckResources.scUnsignedFiles);
                    }
                    Log.WriteMessage(LogVerbosity.Minimum, String.Format(SignCheckResources.scStats, TotalFiles, TotalSignedFiles, TotalUnsignedFiles, TotalSkippedFiles, TotalExcludedFiles));
                }
                else
                {
                    Log.WriteMessage(LogVerbosity.Minimum, SignCheckResources.scNoFilesProcessed);
                }
            }
            catch (Exception e)
            {
                Log.WriteError(e.StackTrace);
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
                using (var wc = new WebClient())
                {
                    var downloadPath = Path.Combine(_appData, Path.GetFileName(uri.LocalPath));

                    if (File.Exists(downloadPath))
                    {
                        Log.WriteMessage(LogVerbosity.Detailed, SignCheckResources.scDeleteExistingFile, downloadPath);
                        File.Delete(downloadPath);
                    }

                    Log.WriteMessage(LogVerbosity.Detailed, SignCheckResources.scDownloading, uri.AbsoluteUri, downloadPath);
                    await wc.DownloadFileTaskAsync(uri, downloadPath);
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

        static void Main(string[] args)
        {
            var sc = new SignCheck(args);
            if ((sc.Options != null) && (!sc.HasArgErrors))
            {
                sc.Run();
            }
        }
    }
}
