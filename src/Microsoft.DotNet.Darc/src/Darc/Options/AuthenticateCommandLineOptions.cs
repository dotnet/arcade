using CommandLine;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("authenticate", HelpText = "Stores the VSTS and GitHub tokens required for remote operations")]
    internal class AuthenticateCommandLineOptions : CommandLineOptions
    {
    }
}
