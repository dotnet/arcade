using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections;

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
        private readonly Dictionary<Guid, TelemetryTaskInfo> _taskTelemetryInfoMap = new Dictionary<Guid, TelemetryTaskInfo>();
        private readonly HashSet<Guid> _detailedLoggedSet = new HashSet<Guid>();
        private HashSet<string> _ignoredTargets;
        private string _solutionDirectory;
        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }
        private static readonly string s_TelemetryMarker = "NETCORE_ENGINEERING_TELEMETRY";
        private static bool _debug = false;

        public void Initialize(IEventSource eventSource)
        {
            if (_debug)
            {
                _debug = false;
                System.Diagnostics.Debugger.Launch();
            }
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
            eventSource.ProjectStarted += OnProjectStarted;
            IEventSource2 eventSource2 = eventSource as IEventSource2;
            eventSource2.TelemetryLogged += OnTelemetryLogged;

            if (Verbosity == LoggerVerbosity.Diagnostic)
            {
                eventSource.ProjectFinished += OnProjectFinished;
            }
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
            string message,
            BuildEventContext buildEventContext)
        {
            var parentId = _buildEventContextMap.TryGetValue(buildEventContext, out var guid)
                ? (Guid?)guid
                : null;
            string telemetryCategory = null;
            State telemetryState = State.Unknown;
            if (parentId.HasValue)
            {
                if(_taskTelemetryInfoMap.TryGetValue(parentId.Value, out TelemetryTaskInfo telemetryInfo))
                {
                    telemetryCategory = telemetryInfo.Category;
                    telemetryState = telemetryInfo.TelemetryState;
                }
                if (telemetryCategory == null)
                {
                    if (_projectInfoMap.TryGetValue(parentId.Value, out ProjectInfo projectInfo))
                    {
                        telemetryCategory = projectInfo.PropertiesCategory;
                    }
                }
                if (telemetryCategory != null)
                {
                    message = $"({s_TelemetryMarker}={telemetryCategory}) {message}";
                }
            }
            if (parentId.HasValue)
            {
                LogDetail(
                    id: parentId.Value,
                    type: s_TelemetryMarker,
                    name: telemetryCategory,
                    state: State.Completed,
                    result: Result.Failed);
            }

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
            Guid? parentId = null,
            string message = null)
        {
            _builder.Start("logdetail");
            _builder.AddProperty("id", id);

            if (parentId != null)
            {
                _builder.AddProperty("parentid", parentId.Value);
            }
            string telemetryCategory = null;
            if (_taskTelemetryInfoMap.TryGetValue(id, out TelemetryTaskInfo telemetryInfo))
            {
                telemetryCategory = telemetryInfo.Category;
            }
            if (telemetryCategory == null)
            {
                if (_projectInfoMap.TryGetValue(id, out ProjectInfo projectInfo))
                {
                    telemetryCategory = projectInfo.PropertiesCategory;
                }
            }
            if (telemetryCategory != null)
            {
                message = $"({s_TelemetryMarker}={telemetryCategory}) {message}";
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

            _builder.Finish(message);

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

        private void LogBuildFinished(
            bool succeeded,
            int threadId,
            BuildEventContext buildEventContext)
        {
            Console.WriteLine($"threadId: {threadId}, succeeded: {succeeded.ToString()}, {buildEventContext?.BuildRequestId}");
        }

        private void LogBuildStarted(
            int threadId,
            IDictionary<string, string> buildEnvironment,
            BuildEventContext buildEventContext)
        {
            Console.WriteLine($"threadId: {threadId}, env count: {buildEnvironment.Count}, {buildEventContext?.BuildRequestId}");
        }
        private void OnErrorRaised(object sender, BuildErrorEventArgs e) =>
            LogErrorOrWarning(isError: true, e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message, e.BuildEventContext);

        private void OnWarningRaised(object sender, BuildWarningEventArgs e) =>
            LogErrorOrWarning(isError: false, e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message, e.BuildEventContext);

        private void OnTelemetryLogged(object sender, TelemetryEventArgs e)
        {
            if (e.EventName.Equals(s_TelemetryMarker))
            {
                e.Properties.TryGetValue("Category", out string telemetryCategory);
                e.Properties.TryGetValue("State", out string telemetryState);
                State state = State.Initialized;
                Enum.TryParse(telemetryState, out state);

                var parentId = _buildEventContextMap.TryGetValue(e.BuildEventContext, out var guid)
                ? (Guid?)guid
                : null;

                if (parentId.HasValue)
                {
                    var telemetryInfo = new TelemetryTaskInfo(parentId.Value, telemetryCategory, state);
                    _taskTelemetryInfoMap[parentId.Value] = telemetryInfo;
                    LogDetail(
                        id: telemetryInfo.Id,
                        type: s_TelemetryMarker,
                        name: telemetryInfo.Category,
                        state: state);
                }
            }
        }
        private void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            if (!_buildEventContextMap.TryGetValue(e.BuildEventContext, out Guid projectId) ||
                !_projectInfoMap.TryGetValue(projectId, out ProjectInfo projectInfo))
            {
                return;
            }
            projectInfo.ProjectState = State.Completed;
            _projectInfoMap[projectId] = projectInfo;

            if (Verbosity == LoggerVerbosity.Diagnostic)
            {
                LogBuildEvent(
                    in projectInfo,
                    State.Completed,
                    result: e.Succeeded ? Result.Succeeded : Result.Failed,
                    startTime: projectInfo.StartTime,
                    endTime: DateTimeOffset.UtcNow,
                    progress: "100");
            }
        }

        private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            if (_ignoredTargets.Contains(e.TargetNames))
            {
                return;
            }

            string propertyCategory = e.Properties?.Cast<DictionaryEntry>().LastOrDefault(p => p.Key.ToString().Equals(s_TelemetryMarker)).Value?.ToString();

            var parentId = _buildEventContextMap.TryGetValue(e.ParentProjectBuildEventContext, out var guid)
            ? (Guid?)guid
            : null;

            var projectInfo = new ProjectInfo(getName(), parentId, propertyCategory, State.Initialized);

            _buildEventContextMap[e.BuildEventContext] = projectInfo.Id;

            _projectInfoMap[projectInfo.Id] = projectInfo;

            if (Verbosity == LoggerVerbosity.Diagnostic)
            {
                LogBuildEvent(
                    in projectInfo,
                    State.Initialized,
                    startTime: projectInfo.StartTime,
                    endTime: null,
                    progress: "0");
            }
            string getName()
            {
                if(Verbosity != LoggerVerbosity.Diagnostic)
                {
                    return string.Empty;
                }
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

        internal readonly struct TelemetryTaskInfo
        {
            internal Guid Id { get; }
            internal string Category { get; }
            internal State TelemetryState { get; }

            internal TelemetryTaskInfo(Guid id, string category, State state = State.Unknown)
            {
                Id = id;
                Category = category;
                TelemetryState = state;
            }
        }

        internal struct ProjectInfo
        {
            internal string Name { get; }
            internal Guid Id { get; }
            internal Guid? ParentId { get; }
            internal DateTimeOffset StartTime { get; }
            internal string PropertiesCategory { get; }
            internal State ProjectState { get; set; }
            internal ProjectInfo(string name, Guid? parentId, string propertiesCategory, State state = State.Unknown)
            {
                Name = name;
                Id = Guid.NewGuid();
                ParentId = parentId;
                StartTime = DateTimeOffset.UtcNow;
                PropertiesCategory = propertiesCategory;
                ProjectState = state;
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
