// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Models;
using Microsoft.DotNet.Darc.Options;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class AuthenticateOperation : Operation
    {
        AuthenticateCommandLineOptions _options;
        public AuthenticateOperation(AuthenticateCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Implements the 'authenticate' verb
        /// </summary>
        /// <param name="options"></param>
        public override Task<int> ExecuteAsync()
        {
            // If clear was passed, then clear the options (no popup)
            if (_options.Clear)
            {
                LocalSettings defaultSettings = new LocalSettings();
                defaultSettings.SaveSettingsFile(Logger);
                return Task.FromResult<int>(Constants.SuccessCode);
            }
            else
            {
                AuthenticateEditorPopUp initEditorPopUp = new AuthenticateEditorPopUp("authenticate-settings/authenticate-todo", Logger);

                UxManager uxManager = new UxManager(Logger);
                return Task.FromResult<int>(uxManager.PopUp(initEditorPopUp));
            }
        }
    }
}
