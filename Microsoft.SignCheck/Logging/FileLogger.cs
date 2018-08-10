using System;
using System.IO;

namespace Microsoft.SignCheck.Logging
{
    public class FileLogger : LoggerBase, ILogger
    {
        internal StreamWriter ErrorWriter
        {
            get;
            set;
        }

        internal StreamWriter MessageWriter
        {
            get;
            set;
        }

        public FileLogger(LogVerbosity verbosity, string messageFile, string errorFile) : base(verbosity)
        {
            if (!String.IsNullOrEmpty(messageFile))
            {
                if (!Directory.Exists(messageFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(messageFile)));
                }
                MessageWriter = File.CreateText(messageFile);
            }

            if (!String.IsNullOrEmpty(errorFile))
            {
                if (!Directory.Exists(errorFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(errorFile)));
                }
                ErrorWriter = File.CreateText(errorFile);
            }
        }

        public override void Close()
        {
            if (MessageWriter != null)
            {
                MessageWriter.Flush();
                MessageWriter.Close();
            }

            if (ErrorWriter != null)
            {
                ErrorWriter.Flush();
                ErrorWriter.Close();
            }
        }

        public void WriteMessage(string message)
        {
            if (MessageWriter != null)
            {
                MessageWriter.WriteLine(message);
            }
        }

        public void WriteMessage(LogVerbosity verbosity, string message)
        {
            if (verbosity <= Verbosity)
            {
                WriteMessage(message);
            }
        }

        public void WriteMessage(LogVerbosity verbosity, string message, params object[] values)
        {
            if (verbosity <= Verbosity)
            {
                WriteMessage(String.Format(message, values));
            }
        }

        public void WriteError(string message)
        {
            HasLoggedErrors = true;
            if (ErrorWriter != null)
            {
                ErrorWriter.WriteLine(message);
            }
        }

        public void WriteError(LogVerbosity verbosity, string message)
        {
            if (verbosity <= Verbosity)
            {
                WriteError(message);
            }
        }

        public void WriteError(LogVerbosity verbosity, string message, params object[] values)
        {
            if (verbosity <= Verbosity)
            {
                WriteError(String.Format(message, values));
            }
        }

        public void WriteLine()
        {
            if (MessageWriter != null)
            {
                MessageWriter.WriteLine();
            }
        }
    }
}
