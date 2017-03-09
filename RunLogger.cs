using System;
using Xunit;

namespace XUnit.Runner.Uap
{
    internal class RunLogger : IRunnerLogger
    {
        private Action<string> loggerAction;

        public RunLogger(Action<string> logger)
        {
            loggerAction = logger;
        }

        readonly object lockObject = new object();

        public object LockObject
        {
            get
            {
                return lockObject;
            }
        }

        public void LogError(StackFrameInfo stackFrame, string message)
        {
            loggerAction($"ERROR:{message}");
        }

        public void LogImportantMessage(StackFrameInfo stackFrame, string message)
        {
            loggerAction($"{message}");
        }

        public void LogMessage(StackFrameInfo stackFrame, string message)
        {
            loggerAction($"{message}");
        }

        public void LogWarning(StackFrameInfo stackFrame, string message)
        {
            loggerAction($"WARNING:{message}");
        }
    }
}
