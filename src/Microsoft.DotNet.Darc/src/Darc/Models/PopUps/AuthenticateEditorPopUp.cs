// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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

        public override bool Validate()
        {
            // No real validation required since none of the fields are mandatory
            return true;
        }

        public override int ProcessContents(IList<Line> contents)
        {
            int result = 0;

            foreach (Line line in contents)
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
                result = Constants.ErrorCode;
            }

            string settings = JsonConvert.SerializeObject(this);

            result = EncodedFile.Create(Constants.SettingsFileName, settings, _logger);

            return 0;
        }
    }
}
