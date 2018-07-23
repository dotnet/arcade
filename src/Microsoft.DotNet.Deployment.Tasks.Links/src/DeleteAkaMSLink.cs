// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Deployment.Tasks.Links
{
    public class DeleteAkaMSLink : AkaMSLinkBase
    {
        [Required]
        public string ShortUrl { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            try
            {
                var response = GetClient().DeleteAsync($"{apiTargetUrl}/{ShortUrl}").Result;
                // Success if it's 202, 204, 404
                if (response.StatusCode != System.Net.HttpStatusCode.NoContent &&
                    response.StatusCode != System.Net.HttpStatusCode.NotFound &&
                    response.StatusCode != System.Net.HttpStatusCode.Accepted)
                {
                    Log.LogError($"Failed to delete aka.ms/{ShortUrl}: {response.Content.ReadAsStringAsync().Result}");
                    return;
                }
                else
                {
                    Log.LogMessage(MessageImportance.Normal, $"Deleted aka.ms/{ShortUrl}");
                    return;
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: true);
            }
        }
    }
}
