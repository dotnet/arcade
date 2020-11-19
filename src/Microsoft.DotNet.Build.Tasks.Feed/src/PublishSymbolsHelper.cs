// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.SymbolUploader;

namespace Microsoft.DotNet.Build.Tasks.Feed
{

    public class PublishSymbolsHelper
    {
        internal static void Publish(
                TaskLoggingHelper log,
                string symbolServerPath,
                string personalAccessToken,
                IEnumerable<string> inputPackages,
                IEnumerable<string> inputFiles,
                HashSet<string> packageExcludeFiles,
                int expirationInDays,
                bool convertPortablePdbsToWindowsPdbs,
                bool publishSpecialClrFiles,
                HashSet<int> pdbConversionTreatAsWarning,
                bool dryRun,
                bool timer,
                bool verboseLogging)
            {
                var tracer = new Tracer(log, verboseLogging);

                PublishOperation publishOperation = new PublishOperation(tracer)
                {
                    SymbolServerPath = symbolServerPath,
                    PersonalAccessToken = personalAccessToken,
                    PdbConversionTreatAsWarning = pdbConversionTreatAsWarning,
                    PublishSpecialClrFiles = publishSpecialClrFiles,
                    Timer = timer,
                };

                using (publishOperation)
                {
                    if (expirationInDays != 0)
                    {
                        publishOperation.ExpirationInDays = (uint) expirationInDays;
                    }

                    IEnumerable<PublishFileInfo> fileInfos = new PublishFileInfo[0];
                    if (inputFiles != null)
                    {
                        fileInfos = fileInfos.Concat(
                            publishOperation.GetPublishFileInfo(inputFiles, convertPortablePdbsToWindowsPdbs));
                    }

                    if (inputPackages != null)
                    {
                        fileInfos = fileInfos.Concat(
                            publishOperation.GetPublishFileInfoFromPackages(inputPackages,
                                convertPortablePdbsToWindowsPdbs));
                    }

                    if (packageExcludeFiles != null)
                    {
                        publishOperation.PackageExcludeFiles = packageExcludeFiles;
                    }

                    if (dryRun)
                    {
                        publishOperation.StartTimer();
                        try
                        {
                            foreach (PublishFileInfo fileInfo in fileInfos)
                            {
                                fileInfo.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            tracer.Error("Dry Run FAILED: {0}", ex.Message);
                            tracer.Information(ex.ToString());
                        }

                        publishOperation.StopTimer();
                    }
                    else
                    {
                        publishOperation.PublishFiles(fileInfos).GetAwaiter().GetResult();
                    }
                }
            }
        }

    internal class Tracer : Microsoft.SymbolStore.ITracer
    {
        readonly TaskLoggingHelper m_log;
        readonly bool m_verbose;

        public Tracer(TaskLoggingHelper log, bool verbose)
        {
            m_log = log;
            m_verbose = verbose;
        }

        public void WriteLine(string message)
        {
            WriteLine("{0}", message);
        }

        public void WriteLine(string format, params object[] arguments)
        {
            m_log.LogMessage(MessageImportance.High, format, arguments);
        }

        public void Information(string message)
        {
            Information("{0}", message);
        }

        public void Information(string format, params object[] arguments)
        {
            m_log.LogMessage(MessageImportance.Normal, format, arguments);
        }

        public void Warning(string message)
        {
            Warning("{0}", message);
        }

        public void Warning(string format, params object[] arguments)
        {
            m_log.LogWarning(format, arguments);
        }

        public void Error(string message)
        {
            Error("{0}", message);
        }

        public void Error(string format, params object[] arguments)
        {
            m_log.LogError(format, arguments);
        }

        public void Verbose(string message)
        {
            Verbose("{0}", message);
        }

        public void Verbose(string format, params object[] arguments)
        {
            m_log.LogMessage(m_verbose ? MessageImportance.Normal : MessageImportance.Low, format, arguments);
        }
    }
}

