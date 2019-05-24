// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Cci.Extensions;
using System;
using System.Diagnostics;
using System.Linq;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.GenFacades.ILRewriter
{
    /// <summary>
    /// Runs GenFacades In-Proc.
    /// </summary>
    public sealed class GenFacadesTask : MSBuild.Task
    {
        [Required]
        public string Seeds { get; set; }

        [Required]
        public string Contracts { get; set; }

        [Required]
        public string FacadePath { get; set; }

        public Version AssemblyFileVersion { get; set; }

        public bool ClearBuildAndRevision { get; set; }

        public bool IgnoreMissingTypes { get; set; }

        public bool IgnoreBuildAndRevisionMismatch { get; set; }

        public bool BuildDesignTimeFacades { get; set; }

        public string InclusionContracts { get; set; }

        public string SeedLoadErrorTreatment { get; set; }

        public string ContractLoadErrorTreatment { get; set; }

        public ITaskItem[] SeedTypePreferencesUnsplit { get; set; }

        public bool ForceZeroVersionSeeds { get; set; }

        public bool ProducePdb { get; set; } = true;

        public string PartialFacadeAssemblyPath { get; set; }

        public bool BuildPartialReferenceFacade { get; set; }

        public override bool Execute()
        {
            TraceLogger logger = new TraceLogger(Log);

            try
            {
                Trace.Listeners.Add(logger);
                ErrorTreatment seedLoadErrorTreatment = ErrorTreatment.Default;
                ErrorTreatment contractLoadErrorTreatment = ErrorTreatment.Default;

                if (SeedLoadErrorTreatment != null)
                {
                    seedLoadErrorTreatment = (ErrorTreatment)Enum.Parse(typeof(ErrorTreatment), SeedLoadErrorTreatment);
                }
                if (ContractLoadErrorTreatment != null)
                {
                    contractLoadErrorTreatment = (ErrorTreatment)Enum.Parse(typeof(ErrorTreatment), ContractLoadErrorTreatment);
                }

                string[] seedTypePreferencesUnsplit = null;
                if (SeedTypePreferencesUnsplit != null)
                {
                    seedTypePreferencesUnsplit = SeedTypePreferencesUnsplit.Select(iti => $"{iti.ItemSpec}={iti.GetMetadata("Assembly")}").ToArray();
                    Trace.WriteLine("seedTypePreferencesUnsplit: " + string.Join(" || ", seedTypePreferencesUnsplit));
                }

                bool result = Generator.Execute(
                    Seeds,
                    Contracts,
                    FacadePath,
                    AssemblyFileVersion,
                    ClearBuildAndRevision,
                    IgnoreMissingTypes,
                    IgnoreBuildAndRevisionMismatch,
                    BuildDesignTimeFacades,
                    InclusionContracts,
                    seedLoadErrorTreatment,
                    contractLoadErrorTreatment,
                    seedTypePreferencesUnsplit,
                    ForceZeroVersionSeeds,
                    ProducePdb,
                    PartialFacadeAssemblyPath,
                    BuildPartialReferenceFacade);

                if (!result)
                {
                    Log.LogError("Errors were encountered when generating facade(s).");
                }

                return result;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: false);
                return false;
            }
            finally
            {
                Trace.Listeners.Remove(logger);
            }
        }
    }

    // Trace listener which writes to the MSBuild log.
    public class TraceLogger : TraceListener
    {
        private readonly TaskLoggingHelper _log;

        public TraceLogger(TaskLoggingHelper log)
        {
            _log = log;
        }

        public override void TraceEvent(TraceEventCache eventCache, String source, TraceEventType eventType, int id, string message)
        {
            TraceEvent(eventCache, source, eventType, id, message, null);
        }

        public override void TraceEvent(TraceEventCache eventCache, String source, TraceEventType eventType, int id, string format, params object[] args)
        {
            string message = args == null ? format : string.Format(format, args);

            if (eventType == TraceEventType.Error)
            {
                // Disabled until we fix warnings -https://github.com/dotnet/corefx/issues/29861
                //_log.LogError(message);
                _log.LogWarning(message);
            }
            else if (eventType == TraceEventType.Warning)
            {
                _log.LogWarning(message);
            }
            else
            {
                _log.LogMessage(message);
            }
        }        

        public override void Write(string message)
        {
            _log.LogMessage(message);
        }

        public override void WriteLine(string message)
        {
            _log.LogMessage(message + Environment.NewLine);
        }
    }
}
