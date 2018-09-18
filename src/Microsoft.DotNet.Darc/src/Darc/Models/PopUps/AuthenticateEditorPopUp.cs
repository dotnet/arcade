using Microsoft.DotNet.Darc.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Microsoft.DotNet.Darc.Models
{
    internal class AuthenticateEditorPopUp : EditorPopUp
    {
        private readonly ILogger _logger;
        private static readonly IList<Line> _contents = new ReadOnlyCollection<Line>(new List<Line>
            {
                new Line("bar_password=<token-from-https://maestro-prod.westus2.cloudapp.azure.com/>"),
                new Line("github_token=<github-personal-access-token>"),
                new Line("vsts_token=<vsts-personal-access-token>"),
                new Line(""),
                new Line("Storing the required tokens...", true),
                new Line("Set 'bar_password', 'github_token' and 'vsts_token' depending on what you need", true),
            });


        public AuthenticateEditorPopUp(string path, ILogger logger)
            : base(path, _contents)
        {
            _logger = logger;
        }

        public string BarPassword { get; set; }

        public string GitHubToken { get; set; }

        public string VstsToken { get; set; }

        public override List<Line> Parse(bool isComment = false)
        {
            return new List<Line>
            {
                new Line(BarPassword, isComment),
                new Line(GitHubToken, isComment),
                new Line(VstsToken, isComment)
            };
        }

        public override bool Validate()
        {
            // No real validation required since none of the fields are mandatory
            return true;
        }

        public override int OnClose(string path)
        {
            int result = 0;

            string[] updatedFileContents = File.ReadAllLines(path);
            List<Line> values = GetContentValues(updatedFileContents);

            foreach (Line line in values)
            {
                string[] keyValue = line.Text.Split("=");

                switch (keyValue[0])
                {
                    case "bar_password":
                        BarPassword = keyValue[1];
                        break;
                    case "github_token":
                        GitHubToken = keyValue[1];
                        break;
                    case "vsts_token":
                        VstsToken = keyValue[1];
                        break;
                    default:
                        _logger.LogWarning($"'{keyValue[0]}' is an unknown field in the authentication scope");
                        break;
                }
            }

            if (!Validate())
            {
                result = -1;
            }

            string settings = JsonConvert.SerializeObject(this);

            result = SecureFile.Create(Constants.SettingsFileName, settings, _logger);

            return 0;
        }
    }
}
