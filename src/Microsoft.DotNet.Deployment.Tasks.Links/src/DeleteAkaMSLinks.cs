// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Deployment.Tasks.Links.src;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Deployment.Tasks.Links
{
    public class DeleteAkaMSLinks : AkaMSLinksBase
    {
        /// <summary>
        /// Set of short urls that should be deleted. Should not include
        /// the "aka.ms/" prefix.
        /// </summary>
        [Required]
        public string[] ShortUrls { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                AkaMSLinkManager manager = new AkaMSLinkManager(ClientId, ClientSecret, Tenant, Log);
                await manager.DeleteLinksAsync(new List<string>(ShortUrls));
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }
            return !Log.HasLoggedErrors;
        }
    }
}
