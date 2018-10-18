// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Maestro.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace Maestro.Web
{
    public partial class Startup
    {
        public const string GitHubScheme = "GitHub";

        public const string MsftAuthorizationPolicyName = "msft";

        private static string ProductName { get; } = "Maestro";

        private static string ProductVersion { get; } = Assembly.GetEntryAssembly().GetName().Version.ToString();

        private void ConfigureAuthServices(IServiceCollection services)
        {
            services.AddIdentity<ApplicationUser, IdentityRole<int>>(
                    options => { options.Lockout.AllowedForNewUsers = false; })
                .AddEntityFrameworkStores<BuildAssetRegistryContext>();

            services.AddSingleton<IAuthenticationSchemeProvider, ContextAwareAuthenticationSchemeProvider>();
            services.AddAuthentication()
                .AddOAuth<GitHubAuthenticationOptions, GitHubAuthenticationHandler>(
                    GitHubScheme,
                    options =>
                    {
                        IConfigurationSection ghAuthConfig = Configuration.GetSection("GitHubAuthentication");
                        ghAuthConfig.Bind(options);
                        options.Events = new OAuthEvents
                        {
                            OnCreatingTicket = async context =>
                            {
                                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                                logger.LogInformation("Reading user roles from GitHub.");
                                foreach (string role in await GetGithubRolesAsync(context.AccessToken))
                                {
                                    context.Identity.AddClaim(
                                        new Claim(ClaimTypes.Role, role, ClaimValueTypes.String, GitHubScheme));
                                }
                            }
                        };
                    })
                .AddPersonalAccessToken<ApplicationUser>(
                    options =>
                    {
                        options.Events = new PersonalAccessTokenEvents<ApplicationUser>
                        {
                            OnSetTokenHash = async context =>
                            {
                                var dbContext = context.HttpContext.RequestServices
                                    .GetRequiredService<BuildAssetRegistryContext>();
                                int userId = context.User.Id;
                                var token = new ApplicationUserPersonalAccessToken
                                {
                                    ApplicationUserId = userId,
                                    Name = context.Name,
                                    Hash = context.Hash,
                                    Created = DateTimeOffset.UtcNow
                                };
                                await dbContext.Set<ApplicationUserPersonalAccessToken>().AddAsync(token);
                                await dbContext.SaveChangesAsync();

                                return token.Id;
                            },
                            OnGetTokenHash = async context =>
                            {
                                var dbContext = context.HttpContext.RequestServices
                                    .GetRequiredService<BuildAssetRegistryContext>();
                                ApplicationUserPersonalAccessToken token = await dbContext
                                    .Set<ApplicationUserPersonalAccessToken>()
                                    .Where(t => t.Id == context.TokenId)
                                    .Include(t => t.ApplicationUser)
                                    .FirstOrDefaultAsync();
                                if (token != null)
                                {
                                    context.Success(token.Hash, token.ApplicationUser);
                                }
                            },
                            OnValidatePrincipal = async context =>
                            {
                                if (ShouldUpdateUser(context.User))
                                {
                                    var dbContext = context.HttpContext.RequestServices
                                        .GetRequiredService<BuildAssetRegistryContext>();
                                    var userManager = context.HttpContext.RequestServices
                                        .GetRequiredService<UserManager<ApplicationUser>>();
                                    var signInManager = context.HttpContext.RequestServices
                                        .GetRequiredService<SignInManager<ApplicationUser>>();

                                    await UpdateUserAsync(context.User, dbContext, userManager, signInManager);

                                    context.ReplacePrincipal(
                                        await signInManager.CreateUserPrincipalAsync(context.User));
                                }
                            }
                        };
                    });
            services.ConfigureExternalCookie(
                options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    options.ReturnUrlParameter = "returnUrl";
                    options.LoginPath = "/Account/SignIn";
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToLogin = ctx =>
                        {
                            if (ctx.Request.Path.StartsWithSegments("/api"))
                            {
                                ctx.Response.StatusCode = 401;
                                return Task.CompletedTask;
                            }

                            ctx.Response.Redirect(ctx.RedirectUri);
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = ctx =>
                        {
                            ctx.Response.StatusCode = 403;
                            return Task.CompletedTask;
                        }
                    };
                });
            services.ConfigureApplicationCookie(
                options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    options.SlidingExpiration = true;
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnSigningIn = ctx =>
                        {
                            IdentityOptions identityOptions = ctx.HttpContext.RequestServices
                                .GetRequiredService<IOptions<IdentityOptions>>()
                                .Value;

                            // replace the ClaimsPrincipal we are about to serialize to the cookie with a reference
                            Claim claim = ctx.Principal.Claims.Single(
                                c => c.Type == identityOptions.ClaimsIdentity.UserIdClaimType);
                            Claim[] claims = {claim};
                            var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
                            ctx.Principal = new ClaimsPrincipal(identity);

                            return Task.CompletedTask;
                        },
                        OnValidatePrincipal = async ctx =>
                        {
                            var dbContext = ctx.HttpContext.RequestServices
                                .GetRequiredService<BuildAssetRegistryContext>();
                            var userManager = ctx.HttpContext.RequestServices
                                .GetRequiredService<UserManager<ApplicationUser>>();
                            var signInManager = ctx.HttpContext.RequestServices
                                .GetRequiredService<SignInManager<ApplicationUser>>();


                            // extract the userId from the ClaimsPrincipal and read the user from the Db
                            ApplicationUser user = await userManager.GetUserAsync(ctx.Principal);
                            if (user == null)
                            {
                                ctx.RejectPrincipal();
                            }
                            else
                            {
                                if (ShouldUpdateUser(user))
                                {
                                    await UpdateUserAsync(user, dbContext, userManager, signInManager);
                                }

                                ClaimsPrincipal principal = await signInManager.CreateUserPrincipalAsync(user);
                                ctx.ReplacePrincipal(principal);
                            }
                        }
                    };
                });

            services.AddAuthorization(
                options =>
                {
                    options.AddPolicy(
                        MsftAuthorizationPolicyName,
                        policy =>
                        {
                            policy.RequireAuthenticatedUser();
                            if (!HostingEnvironment.IsDevelopment())
                            {
                                policy.RequireRole("github:team:dotnet:dnceng", "github:team:dotnet:arcade-contrib");
                            }
                        });
                });

            services.Configure<MvcOptions>(
                options =>
                {
                    options.Conventions.Add(new DefaultAuthorizeActionModelConvention(MsftAuthorizationPolicyName));
                });
        }

        private bool ShouldUpdateUser(ApplicationUser user)
        {
            // If we haven't updated the user in the last 30 minutes
            return DateTimeOffset.UtcNow - user.LastUpdated > new TimeSpan(0, 30, 0);
        }

        private async Task UpdateUserAsync(
            ApplicationUser user,
            BuildAssetRegistryContext dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            using (IDbContextTransaction txn = await dbContext.Database.BeginTransactionAsync())
            {
                string token = await userManager.GetAuthenticationTokenAsync(user, GitHubScheme, "access_token");
                var roles = new HashSet<string>(await GetGithubRolesAsync(token));
                List<Claim> currentRoles = (await userManager.GetClaimsAsync(user))
                    .Where(c => c.Type == ClaimTypes.Role)
                    .ToList();

                // remove claims where github doesn't have the role anymore
                await userManager.RemoveClaimsAsync(user, currentRoles.Where(c => !roles.Contains(c.Value)));

                // add new claims
                await userManager.AddClaimsAsync(
                    user,
                    roles.Where(r => currentRoles.All(c => c.Value != r))
                        .Select(r => new Claim(ClaimTypes.Role, r, ClaimValueTypes.String, GitHubScheme)));

                user.LastUpdated = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync();
                txn.Commit();
            }
        }

        private static async Task<IList<string>> GetGithubRolesAsync(string accessToken)
        {
            var client =
                new GitHubClient(new ProductHeaderValue(ProductName, ProductVersion))
                {
                    Credentials = new Credentials(accessToken)
                };
            return (await client.Organization.GetAllForCurrent()).Select(org => $"github:org:{org.Login}")
                .Concat(
                    (await client.Organization.Team.GetAllForCurrent()).Select(
                        team => $"github:team:{team.Organization.Login}:{team.Name}"))
                .ToList();
        }
    }
}
