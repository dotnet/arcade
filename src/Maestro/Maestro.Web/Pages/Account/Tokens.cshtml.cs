// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Maestro.Web.Pages.Account
{
    public class TokensModel : PageModel
    {
        public TokensModel(BuildAssetRegistryContext context, UserManager<ApplicationUser> userManager)
        {
            Context = context;
            UserManager = userManager;
        }

        public BuildAssetRegistryContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }

        public List<TokenModel> Tokens { get; set; }

        [BindProperty]
        public string TokenName { get; set; }

        public string CreatedTokenValue { get; set; }

        public string Error { get; set; }

        public async Task<IActionResult> OnGet()
        {
            ApplicationUser user = await UserManager.GetUserAsync(User);
            Tokens = await GetTokens(user);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteTokenAsync()
        {
            ApplicationUser user = await UserManager.GetUserAsync(User);
            try
            {
                ApplicationUserPersonalAccessToken token = await Context.Set<ApplicationUserPersonalAccessToken>()
                    .Where(t => t.ApplicationUserId == user.Id && t.Name == TokenName)
                    .SingleAsync();
                Context.Remove(token);
                await Context.SaveChangesAsync();
            }
            catch (Exception)
            {
                Error = "Unable to delete token.";
            }

            Tokens = await GetTokens(user);
            return Page();
        }

        public async Task<IActionResult> OnPostNewTokenAsync()
        {
            ApplicationUser user = await UserManager.GetUserAsync(User);
            Tokens = await GetTokens(user);
            try
            {
                CreatedTokenValue = await HttpContext.CreatePersonalAccessTokenAsync(user, TokenName);
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

            return Page();
        }

        private async Task<List<TokenModel>> GetTokens(ApplicationUser user)
        {
            await Context.Entry(user).Collection(u => u.PersonalAccessTokens).LoadAsync();
            return user.PersonalAccessTokens.Select(t => new TokenModel {Name = t.Name, Created = t.Created}).ToList();
        }

        public class TokenModel
        {
            public string Name { get; set; }
            public DateTimeOffset Created { get; set; }
        }
    }
}
