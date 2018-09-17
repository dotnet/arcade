using CommandLine;

namespace Microsoft.DotNet.Darc
{
    abstract class CommandLineOptions
    {
        [Option('p', "pat", HelpText = "Github PAT used to authenticate against Maestro/BAR REST APIs if necessary.")]
        public string Pat { get; set; }

        [Option("verbose", HelpText = "Turn on verbose output")]
        public bool Verbose { get; set; }

        [Option("debug", HelpText = "Turn on debug output")]
        public bool Debug { get; set; }

        public string LocalDirectory { get { return System.IO.Directory.GetCurrentDirectory(); } }
    }

    [Verb("get", HelpText = "Query information about dependencies")]
    internal class GetCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", Required = true, HelpText = "Name of dependency to query for")]
        string Name { get; set; }
    }

    [Verb("add", HelpText = "Add a new dependency to version.details.xml")]
    internal class AddCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", Required = true, HelpText = "Name of dependency to add.")]
        string Name { get; set; }
    }
}
