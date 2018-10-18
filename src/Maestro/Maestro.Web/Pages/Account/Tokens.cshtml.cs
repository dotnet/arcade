// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maestro.Web.Pages.Account
{
    public class TokensModel : PageModel
    {
        public TokensModel(
            BuildAssetRegistryContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<TokensModel> logger)
        {
            Context = context;
            UserManager = userManager;
            Logger = logger;
        }

        public BuildAssetRegistryContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public ILogger<TokensModel> Logger { get; }

        public List<TokenModel> Tokens { get; private set; }

        [BindProperty]
        public string TokenName { get; set; }

        [BindProperty]
        public int TokenId { get; set; }

        [TempData]
        public int CreatedTokenId { get; set; }

        [TempData]
        public string CreatedTokenValue { get; set; }

        [TempData]
        public string Message { get; set; }

        [TempData]
        public string Error { get; set; }

        public async Task<IActionResult> OnGet()
        {
            ApplicationUser user = await UserManager.GetUserAsync(User);
            Tokens = await GetTokens(user);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteTokenAsync()
        {
            if (TokenId < 1)
            {
                return BadRequest("Invalid Token Id");
            }

            ApplicationUser user = await UserManager.GetUserAsync(User);
            try
            {
                ApplicationUserPersonalAccessToken token = await Context.Set<ApplicationUserPersonalAccessToken>()
                    .Where(t => t.ApplicationUserId == user.Id && t.Id == TokenId)
                    .SingleAsync();
                Context.Remove(token);
                await Context.SaveChangesAsync();
                Message = "Token deleted.";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to delete token.");
                Error = "Unable to delete token.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostNewTokenAsync()
        {
            ApplicationUser user = await UserManager.GetUserAsync(User);
            try
            {
                (CreatedTokenId, CreatedTokenValue) = await HttpContext.CreatePersonalAccessTokenAsync(user, TokenName);
            }
            catch (DbUpdateException dbEx) when (dbEx.InnerException is SqlException sqlEx &&
                                                 sqlEx.Message.Contains("Cannot insert duplicate key row"))
            {
                Error = "A token with that name already exists.";
            }
            catch (Exception)
            {
                Error = "Unable to create token.";
            }

            if (Error != null)
            {
                return Page();
            }

            Message = "Make sure to copy your new personal access token now. You won't be able to see it again!";

            return RedirectToPage();
        }

        private async Task<List<TokenModel>> GetTokens(ApplicationUser user)
        {
            await Context.Entry(user).Collection(u => u.PersonalAccessTokens).LoadAsync();
            return user.PersonalAccessTokens.Select(
                    t => new TokenModel
                    {
                        Name = t.Name,
                        Id = t.Id,
                        Created = t.Created
                    })
                .ToList();
        }

        public class TokenModel
        {
            public string Name { get; set; }
            public int Id { get; set; }
            public DateTimeOffset Created { get; set; }
        }
    }
}
