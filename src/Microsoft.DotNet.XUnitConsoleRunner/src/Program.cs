using System;
using System.Linq;

namespace Xunit.ConsoleClient
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // This code must not contain any references to code in any external assembly (or any code that references any code in any
            // other assembly) until AFTER the creation of the AssemblyHelper.
            var consoleLock = new object();
            var internalDiagnosticsMessageSink = DiagnosticMessageSink.ForInternalDiagnostics(consoleLock, args.Contains("-internaldiagnostics"), args.Contains("-nocolor"));

#if !WINDOWS_UWP
            using (AssemblyHelper.SubscribeResolveForAssembly(typeof(Program), internalDiagnosticsMessageSink))
#endif
                return new ConsoleRunner(consoleLock).EntryPoint(args);
        }
    }
}
