// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider
{
    public class ValidRequestHandler : AuthorizationHandler<ValidRequestRequirement>
    {
        private IHttpContextAccessor _httpContextAccessor;
        private const string signatureHeaderEntry = "X-Azure-Signature";

        public ValidRequestHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ValidRequestRequirement requirement)
        {
            HttpContext httpContext = _httpContextAccessor.HttpContext;
            httpContext.Request.EnableBuffering();

            var signature = httpContext.Request.Headers[signatureHeaderEntry];
            if (IsValidRequestSource(requirement.SharedSecret, signature, httpContext.Request.Body))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// This is for verification of the Azure DevOps instance against the pool provider
        /// </summary>
        /// <returns>True if the request source is valid, false otherwise.</returns>
        private bool IsValidRequestSource(string sharedSecret, string signature, Stream requestBody)
        {
            // Convert the shared secrets to bytes (UTF8).
            byte[] sharedSecretBytes = Encoding.UTF8.GetBytes(sharedSecret);
            // Use this as an invarant in HMACSHA512 and compare against the header signature
            HMACSHA512 hmac = new HMACSHA512(sharedSecretBytes);
            // Compute the hash using the full request body.
            byte[] hashBytes = hmac.ComputeHash(requestBody);
            // Set the position back to zero for good measure.
            requestBody.Position = 0;
            // Convert to hex and compare to request header.  Note that it's more convenient here to
            // convert the byte array back into a hex string and compare vs. converting the original
            // header string into the appropriate byte array.  Core currently doesn't have a string->byte array
            // conversion.
            string hashString = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
            return hashString.Equals(signature, StringComparison.OrdinalIgnoreCase);
        }
    }
}
