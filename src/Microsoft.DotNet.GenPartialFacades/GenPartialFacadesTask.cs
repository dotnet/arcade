// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Diagnostics;
using System.Linq;
using Microsoft.Cci.Extensions;

namespace Microsoft.DotNet.GenPartialFacades
{
    public class GenPartialFacadesTask : Task
    {
        [Required]
        public string Seeds { get; set; }

        [Required]
        public string Contracts { get; set; }

        [Required]
        public string CompileFiles { get; set; }

        public string Constants { get; set; }

        public bool IgnoreMissingTypes { get; set; }

        public string InclusionContracts { get; set; }

        [Required]
        public string ContractAssemblyName { get; set; }

        public ITaskItem[] SeedTypePreferencesUnsplit { get; set; }

        public string SeedLoadErrorTreatment { get; set; }

        public string ContractLoadErrorTreatment { get; set; }

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

                string[] typeAssemblyAlias = null;
                if (SeedTypePreferencesUnsplit != null)
                {
                    typeAssemblyAlias = SeedTypePreferencesUnsplit.Select(iti => $"{iti.ItemSpec}={iti.GetMetadata("Alias")}").ToArray();
                    Trace.WriteLine("typeAssemblyAlias: " + string.Join(" || ", typeAssemblyAlias));
                }

                bool result = GenPartialFacadesGenerator.Execute(
                    Seeds,
                    Contracts,
                    CompileFiles,
                    Constants,
                    ContractAssemblyName,
                    IgnoreMissingTypes,
                    InclusionContracts,
                    seedTypePreferencesUnsplit,
                    typeAssemblyAlias);

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
