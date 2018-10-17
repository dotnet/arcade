// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Maestro.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Maestro.Web.Pages.Account
{
    public class AccountController : Controller
    {
        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            BuildAssetRegistryContext context)
        {
            SignInManager = signInManager;
            UserManager = userManager;
            Context = context;
        }

        public SignInManager<ApplicationUser> SignInManager { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public BuildAssetRegistryContext Context { get; }

        [HttpGet("/Account/SignOut")]
        [AllowAnonymous]
        public async Task<IActionResult> SignOut()
        {
            await SignInManager.SignOutAsync();
            return RedirectToPage("/Index");
        }

        [HttpGet("/Account/SignIn")]
        [AllowAnonymous]
        public IActionResult SignIn(string returnUrl = null)
        {
            string redirectUrl = Url.Action(nameof(LogInCallback), "Account", new {returnUrl});
            AuthenticationProperties properties =
                SignInManager.ConfigureExternalAuthenticationProperties(Startup.GitHubScheme, redirectUrl);
            return Challenge(properties, Startup.GitHubScheme);
        }

        [HttpGet("/Account/LogInCallback")]
        [AllowAnonymous]
        public async Task<IActionResult> LogInCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                return StatusCode(400, $"Error loging in: {remoteError}");
            }

            ExternalLoginInfo info = await SignInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return StatusCode(400, "Unable to Sign In");
            }

            SignInResult signInResult =
                await SignInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, true);
            if (signInResult.Succeeded)
            {
                return RedirectToLocal(returnUrl);
            }

            if (!signInResult.IsLockedOut)
            {
                ApplicationUser user = await CreateUserAsync(info);
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, true);
                    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                    return RedirectToLocal(returnUrl);
                }
            }

            return StatusCode(403);
        }

        private async Task<ApplicationUser> CreateUserAsync(ExternalLoginInfo info)
        {
            string id = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            string name = info.Principal.FindFirstValue(ClaimTypes.Name);
            string fullName = info.Principal.FindFirstValue("urn:github:name");
            string accessToken = info.AuthenticationTokens.First(t => t.Name == "access_token").Value;

            using (IDbContextTransaction txn = await Context.Database.BeginTransactionAsync())
            {
                var user = new ApplicationUser
                {
                    UserName = name,
                    FullName = fullName
                };
                IdentityResult result = await UserManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    return null;
                }

                result = await UserManager.SetAuthenticationTokenAsync(
                    user,
                    info.LoginProvider,
                    "access_token",
                    accessToken);
                if (!result.Succeeded)
                {
                    return null;
                }

                IEnumerable<Claim> claimsToAdd = info.Principal.Claims.Where(
                    c => c.Type == ClaimTypes.Email || c.Type == "urn:github:name" || c.Type == "urn:github:url" ||
                         c.Type == ClaimTypes.Role);

                result = await UserManager.AddClaimsAsync(user, claimsToAdd);
                if (!result.Succeeded)
                {
                    return null;
                }

                result = await UserManager.AddLoginAsync(user, info);
                if (!result.Succeeded)
                {
                    return null;
                }

                txn.Commit();
                return user;
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect("/");
        }
    }
}
