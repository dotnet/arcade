using CommandLine;
using Microsoft.DotNet.Darc.Options;

namespace Microsoft.DotNet.Darc
{
    class Program
    {
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<AuthenticateCommandLineOptions, GetCommandLineOptions, AddCommandLineOptions>(args)
                .MapResult(
                    (AuthenticateCommandLineOptions opts) => Operations.AuthenticateOperation(opts),
                    (GetCommandLineOptions opts) => Operations.GetOperation(opts),
                    (AddCommandLineOptions opts) => Operations.AddOperation(opts),
                    (errs => 1));
        }
    }
}
