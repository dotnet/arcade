// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.ArcadeLogging
{
    /// <summary>
    /// Logger that translates MSBuild errors and warnings into the Azure Pipelines task
    /// logging format so that they surface as issues on the build timeline.
    ///
    /// https://github.com/Microsoft/azure-pipelines-tasks/blob/601dd2f0a3e671b19b55bcf139f554a09f3414da/docs/authoring/commands.md
    /// </summary>
    public sealed class PipelinesLogger : ILogger
    {
        private readonly MessageBuilder _builder = new MessageBuilder();

        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            eventSource.ErrorRaised += OnErrorRaised;
            eventSource.WarningRaised += OnWarningRaised;
        }

        public void Shutdown()
        {
        }

        private void LogIssue(
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

        private void OnErrorRaised(object sender, BuildErrorEventArgs e) =>
            LogIssue(isError: true, e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message);

        private void OnWarningRaised(object sender, BuildWarningEventArgs e) =>
            LogIssue(isError: false, e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message);
    }
}
