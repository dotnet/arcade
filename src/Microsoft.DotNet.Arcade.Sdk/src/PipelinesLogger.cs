using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Arcade.Sdk
{
    /// <summary>
    /// Logger for converting MSBuild error messages to the Azure Pipelines Tasks format
    /// 
    /// https://github.com/Microsoft/azure-pipelines-tasks/blob/601dd2f0a3e671b19b55bcf139f554a09f3414da/docs/authoring/commands.md
    /// </summary>
    public sealed class PipelinesLogger : ILogger
    {
        private readonly MessageBuilder _builder = new MessageBuilder();
        private readonly Dictionary<BuildEventContext, Guid> _buildEventContextMap = new Dictionary<BuildEventContext, Guid>(BuildEventContextComparer.Instance);
        private readonly Dictionary<Guid, ProjectInfo> _projectInfoMap = new Dictionary<Guid, ProjectInfo>();
        private readonly Dictionary<BuildEventContext, string> _targetsMap = new Dictionary<BuildEventContext, string>();
        private readonly HashSet<Guid> _detailedLoggedSet = new HashSet<Guid>();
        private readonly HttpClient _http = new HttpClient();
        private HashSet<string> _ignoredTargets;
        private string _solutionDirectory;

        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            var parameters = LoggerParameters.Parse(this.Parameters);

            _solutionDirectory = parameters["SolutionDir"];

            var verbosityString = parameters["Verbosity"];
            Verbosity = !string.IsNullOrEmpty(verbosityString) && Enum.TryParse(verbosityString, out LoggerVerbosity verbosity)
                ? verbosity
                : LoggerVerbosity.Normal;

            var ignoredTargets = new string[]
            {
                "GetCopyToOutputDirectoryItems",
                "GetNativeManifest",
                "GetTargetPath",
                "GetTargetFrameworks",
            };
            _ignoredTargets = new HashSet<string>(ignoredTargets, StringComparer.OrdinalIgnoreCase);

            // TargetsNotLogged is an optional parameter.
            var targetsNotLogged = parameters["TargetsNotLogged"];
            if (!string.IsNullOrEmpty(targetsNotLogged))
            {
                _ignoredTargets.UnionWith(targetsNotLogged.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            }

            eventSource.ErrorRaised += OnErrorRaised;
            eventSource.WarningRaised += OnWarningRaised;

            if (Verbosity == LoggerVerbosity.Diagnostic)
            {
                eventSource.ProjectFinished += OnProjectFinished;
                eventSource.ProjectStarted += OnProjectStarted;
            }

            eventSource.ProjectStarted += RecordTargets;
            eventSource.ProjectFinished += ReportToAnalytics;
        }

        public void Shutdown()
        {

        }

        private void LogErrorOrWarning(
            bool isError,
            string sourceFilePath,
            int line,
            int column,
            string code,
            string message)
        {
            _builder.Start("logissue");
            _builder.AddProperty("type", isError ? "error" : "warning");
            _builder.AddProperty("sourcepath", sourceFilePath);
            _builder.AddProperty("linenumber", line);
            _builder.AddProperty("columnnumber", column);
            _builder.AddProperty("code", code);
            _builder.Finish(message);
            Console.WriteLine(_builder.GetMessage());
        }

        private void LogDetail(
            Guid id, 
            string type,
            string name,
            State state,
            Result? result = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string order = null,
            string progress = null,
            Guid? parentId = null)
        {
            _builder.Start("logdetail");
            _builder.AddProperty("id", id);

            if (parentId != null)
            {
                _builder.AddProperty("parentid", parentId.Value);
            }

            // Certain values on logdetail can only be set once by design of VSO
            if (_detailedLoggedSet.Add(id))
            {
                _builder.AddProperty("type", type);
                _builder.AddProperty("name", name);

                if (!string.IsNullOrEmpty(order))
                {
                    _builder.AddProperty("order", order);
                }
            }

            if (startTime.HasValue)
            {
                _builder.AddProperty("starttime", startTime.Value);
            }

            if (endTime.HasValue)
            {
                _builder.AddProperty("endtime", endTime.Value);
            }

            if (!string.IsNullOrEmpty(progress))
            {
                _builder.AddProperty("progress", progress);
            }

            _builder.AddProperty("state", state.ToString());
            if (result.HasValue)
            {
                _builder.AddProperty("result", result.Value.ToString());
            }

            _builder.Finish();

            Console.WriteLine(_builder.GetMessage());
        }

        private void LogBuildEvent(
            in ProjectInfo projectInfo,
            State state,
            Result? result = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string order = null,
            string progress = null) =>
            LogDetail(
                id: projectInfo.Id,
                type: "Build",
                name: projectInfo.Name,
                state: state,
                result: result,
                startTime: startTime,
                endTime: endTime,
                progress: progress,
                order: order,
                parentId: projectInfo.ParentId);

        private void OnErrorRaised(object sender, BuildErrorEventArgs e) =>
            LogErrorOrWarning(isError: true, e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message);

        private void OnWarningRaised(object sender, BuildWarningEventArgs e) =>
            LogErrorOrWarning(isError: false, e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message);

        private void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            if (!_buildEventContextMap.TryGetValue(e.BuildEventContext, out Guid projectId) ||
                !_projectInfoMap.TryGetValue(projectId, out ProjectInfo projectInfo))
            {
                return;
            }

            LogBuildEvent(
                in projectInfo,
                State.Completed,
                result: e.Succeeded ? Result.Succeeded : Result.Failed,
                startTime: projectInfo.StartTime,
                endTime: DateTimeOffset.UtcNow,
                progress: "100");
        }

        private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            if (_ignoredTargets.Contains(e.TargetNames))
            {
                return;
            }

            var parentId = _buildEventContextMap.TryGetValue(e.ParentProjectBuildEventContext, out var guid)
                ? (Guid?)guid
                : null;
            var projectInfo = new ProjectInfo(getName(), parentId);
            _projectInfoMap[projectInfo.Id] = projectInfo;
            _buildEventContextMap[e.BuildEventContext] = projectInfo.Id;
            LogBuildEvent(
                in projectInfo,
                State.Initialized,
                startTime: projectInfo.StartTime,
                endTime: null,
                progress: "0");

            string getName()
            {
                // Note, website projects (sln file only, no proj file) emit a started event with projectFile == $"{m_solutionDirectory}\\".
                // This causes issues when attempting to get the relative path (and also Path.GetFileName returns empty string).
                var projectFile = e.ProjectFile;
                projectFile = (projectFile ?? string.Empty).TrimEnd('\\');

                // Make the name relative.
                if (!string.IsNullOrEmpty(_solutionDirectory) &&
                    projectFile.StartsWith(_solutionDirectory + @"\", StringComparison.OrdinalIgnoreCase))
                {
                    projectFile = projectFile.Substring(_solutionDirectory.Length).TrimStart('\\');
                }
                else
                {
                    try
                    {
                        projectFile = Path.GetFileName(projectFile);
                    }
                    catch (Exception)
                    {
                    }
                }

                // Default the project file.
                if (string.IsNullOrEmpty(projectFile))
                {
                    projectFile = "Unknown";
                }

                string targetFrameworkQualifier = string.Empty;
                if (e.GlobalProperties.TryGetValue("TargetFramework", out string targetFramework))
                {
                    targetFrameworkQualifier = $" - {targetFramework}";
                }

                string targetNamesQualifier = string.IsNullOrEmpty(e.TargetNames) ? string.Empty : $" ({e.TargetNames})";

                return projectFile + targetFrameworkQualifier + targetNamesQualifier;
            }
        }

        private void RecordTargets(object sender, ProjectStartedEventArgs e)
        {
            _targetsMap[e.BuildEventContext] = e.TargetNames;
        }

        private void ReportToAnalytics(object sender, ProjectFinishedEventArgs projectFinished)
        {
            try
            {
                string agentName = Environment.GetEnvironmentVariable("AGENT_NAME");
                string buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID");
                string buildName = Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER");
                string repositoryUri = Environment.GetEnvironmentVariable("BUILD_REPOSITORY_URI");
                string sourceBranch = Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH");

                if (string.IsNullOrEmpty(agentName) ||
                    string.IsNullOrEmpty(buildId) ||
                    string.IsNullOrEmpty(buildName) ||
                    string.IsNullOrEmpty(repositoryUri) ||
                    string.IsNullOrEmpty(sourceBranch))
                {
                    return;
                }

                if (!_targetsMap.TryGetValue(projectFinished.BuildEventContext, out var targets))
                {
                    targets = "--";
                }

                var eventTelemetryEnvelope = new JObject
                {
                    {"name", "Microsoft.ApplicationInsights.Event"},
                    {"time", DateTime.UtcNow.ToString("O")},
                    {"iKey", "82db48c2-1490-471b-a273-d7ac4585c8e8"},
                    {"flags", 0x200000},
                    {
                        "tags",
                        new JObject
                        {
                            {"ai.cloud.roleInstance", agentName}
                        }
                    },
                    {
                        "data",
                        new JObject
                        {
                            {"baseType", "EventData"},
                            {
                                "baseData",
                                new JObject
                                {
                                    {"ver", 2},
                                    {"name", "buildResults"},
                                    {
                                        "properties",
                                        new JObject
                                        {
                                            {"branch", sourceBranch},
                                            {"buildId", buildId},
                                            {"buildName", buildName},
                                            {"project", projectFinished.ProjectFile ?? "Unknown"},
                                            {"repository", repositoryUri},
                                            {"succeeded", projectFinished.Succeeded.ToString()},
                                            {"targets", targets},
                                        }
                                    },
                                    {"measurements", new JObject()},
                                }
                            }
                        }
                    }
                };

                HttpResponseMessage response = _http.PostAsync(
                        "https://dc.services.visualstudio.com/v2/track",
                        new StringContent(eventTelemetryEnvelope.ToString(Formatting.None),
                            Encoding.UTF8,
                            "application/json")
                    )
                    .GetAwaiter()
                    .GetResult();

                // This analytics call isn't critical enough to do anything about, we'll get enough of them in aggregate to serve our purposes
                // so don't check the response code, just dispose it
                response.Dispose();
            }
            catch (Exception ex)
            {
                // Not anything useful we can do here.
                _builder.Start("logissue");
                _builder.AddProperty("type", "warning");
                _builder.AddProperty("code", "AI0001");
                _builder.Finish(ex.Message);
                Console.WriteLine(_builder.GetMessage());
            }
        }

        internal sealed class LoggerParameters
        {
            internal const char NameValueDelimiter = '=';
            internal const char NameValuePairDelimiter = '|';
            internal static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

            private readonly Dictionary<string, string> _parameters;

            public string this[string name] => _parameters.TryGetValue(name, out var value) ? value : string.Empty;

            public LoggerParameters(Dictionary<string, string> parameters)
            {
                _parameters = parameters;
            }

            public static LoggerParameters Parse(string paramString)
            {
                if (string.IsNullOrEmpty(paramString))
                {
                    return new LoggerParameters(new Dictionary<string, string>(Comparer));
                }

                // split the given string into name1 = value1 | name2 = value2
                string[] nameValuePairs = paramString.Split(NameValuePairDelimiter);
                var parameters = new Dictionary<string, string>(Comparer);
                foreach (string str in nameValuePairs)
                {
                    // look for the = char. URI's are value and can have = in them.
                    int valueDelimiterIndex = str.IndexOf(NameValueDelimiter);
                    if (valueDelimiterIndex >= 0)
                    {
                        // get the 2 strings.
                        string name = str.Substring(0, valueDelimiterIndex);
                        string value = str.Substring(valueDelimiterIndex + 1);
                        parameters.Add(name.Trim(), value.Trim());
                    }
                }

                return new LoggerParameters(parameters);
            }
        }

        internal readonly struct ProjectInfo
        {
            internal string Name { get; }
            internal Guid Id { get; }
            internal Guid? ParentId { get; }
            internal DateTimeOffset StartTime { get; }

            internal ProjectInfo(string name, Guid? parentId)
            {
                Name = name;
                Id = Guid.NewGuid();
                ParentId = parentId;
                StartTime = DateTimeOffset.UtcNow;
            }
        }

        internal enum State
        {
            Unknown,
            Initialized,
            InProgress,
            Completed,
        }

        internal enum Result
        {
            Succeeded,
            SucceededWithIssues,
            Failed,
            Canceled,
            Skipped,
        }

        internal sealed class MessageBuilder
        {
            internal enum State
            {
                NotStarted,
                Properties,
                Finished,
            }

            private readonly StringBuilder _builder = new StringBuilder();
            private State _state;

            internal void Start(string kind)
            {
                Debug.Assert(_state == State.NotStarted || _state == State.Finished);
                _builder.Length = 0;
                _builder.Append($"##vso[task.{kind} ");
                _state = State.Properties;
            }

            internal void AddProperty(string name, string value)
            {
                Debug.Assert(_state == State.Properties);

                _builder.Append($"{name}={Escape(value)};");
            }

            internal void AddProperty(string name, DateTimeOffset value) => AddProperty(name, value.ToString("O"));
            internal void AddProperty(string name, int value) => AddProperty(name, value.ToString());
            internal void AddProperty(string name, Guid value) => AddProperty(name, value.ToString("D"));

            internal void Finish(string message = null)
            {
                Debug.Assert(_state == State.Properties);
                _builder.Append("]");
                if (!string.IsNullOrEmpty(message))
                {
                    _builder.Append(Escape(message));
                }

                _state = State.Finished;
            }

            internal string GetMessage()
            {
                Debug.Assert(_state == State.Finished);
                return _builder.ToString();
            }

            private static string Escape(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return string.Empty;
                }

                var result = new StringBuilder(value.Length);
                foreach (char c in value)
                {
                    switch (c)
                    {
                        case ';':
                            result.Append("%3B");
                            break;
                        case '\r':
                            result.Append("%0D");
                            break;
                        case '\n':
                            result.Append("%0A");
                            break;
                        case ']':
                            result.Append("%5D");
                            break;
                        default:
                            result.Append(c);
                            break;
                    }
                }

                return result.ToString();
            }
        }

        /// <summary>
        /// Compares two event contexts on ProjectContextId and NodeId only.
        /// NOTE: Copied from MSBuild ParallelLoggerHelpers.cs.
        /// </summary>
        internal sealed class BuildEventContextComparer : IEqualityComparer<BuildEventContext>
        {
            public static BuildEventContextComparer Instance { get; } = new BuildEventContextComparer();

            public bool Equals(BuildEventContext x, BuildEventContext y) =>
                x.NodeId == y.NodeId &&
                x.ProjectContextId == y.ProjectContextId;

            // This gives the low 24 bits to ProjectContextId and the high 8 to NodeId.  
            public int GetHashCode(BuildEventContext x) => x.ProjectContextId + (x.NodeId << 24);
        }
    }
}
