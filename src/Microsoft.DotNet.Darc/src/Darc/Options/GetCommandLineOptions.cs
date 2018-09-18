using CommandLine;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("get", HelpText = "Query information about dependencies")]
    internal class GetCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", Required = true, HelpText = "Name of dependency to query for")]
        string Name { get; set; }
    }
}
