namespace Microsoft.SignCheck.Logging
{
    public interface ILogger
    {
        bool HasLoggedErrors
        {
            get;
        }

        void Close();

        void WriteMessage(string message);

        void WriteMessage(LogVerbosity verbosity, string message);

        void WriteMessage(LogVerbosity verbosity, string message, params object[] values);

        void WriteLine();

        void WriteError(string message);

        void WriteError(LogVerbosity verbosity, string message);

        void WriteError(LogVerbosity verbosity, string message, params object[] values);
    }
}
