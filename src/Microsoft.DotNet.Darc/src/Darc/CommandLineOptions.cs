using CommandLine;

namespace Microsoft.DotNet.Darc
{
    abstract class CommandLineOptions
    {
        [Option('p', "pat", HelpText = "Github PAT used to authenticate against Maestro/BAR REST APIs if necessary.")]
        string Pat { get; set; }

        public string LocalDirectory { get { return System.IO.Directory.GetCurrentDirectory(); } }
    }

    [Verb("get", HelpText = "Query information about dependencies")]
    internal class GetCommandLineOptions : CommandLineOptions
    {
        public string Pat { get; set; }
    }

    [Verb("add", HelpText = "Add a new dependency to version.details.xml")]
    internal class AddCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", Required = true, HelpText = "Name of dependency to add.")]
        string Name { get; set; }
    }
}
