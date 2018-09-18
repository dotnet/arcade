using CommandLine;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("add", HelpText = "Add a new dependency to Version.Details.xml")]
    internal class AddCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", Required = true, HelpText = "Name of dependency to add.")]
        string Name { get; set; }
    }
}
