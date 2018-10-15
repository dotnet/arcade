namespace Microsoft.SignCheck.Logging
{
    public abstract class LoggerBase
    {
        protected LogVerbosity Verbosity
        {
            get;
            set;
        }

        public bool HasLoggedErrors
        {
            get;
            protected set;
        }

        public LoggerBase(LogVerbosity verbosity)
        {
            Verbosity = verbosity;
        }

        public virtual void Close()
        {

        }
    }
}
