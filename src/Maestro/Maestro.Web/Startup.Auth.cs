using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Maestro.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octokit;

namespace Maestro.Web
{
    public partial class Startup
    {
        public const string GitHubScheme = "GitHub";

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
                        IConfiguration ghAuthConfig;
                        if (HostingEnvironment.IsDevelopment() && Program.RunningInServiceFabric())
                        {
                            ghAuthConfig = Configuration.GetSection("GitHubAuthentication-SvcFabDev");
                        }
                        else
                        {
                            ghAuthConfig = Configuration.GetSection("GitHubAuthentication");
                        }

                        options.ClientId = ghAuthConfig["ClientId"];
                        options.ClientSecret = ghAuthConfig["ClientSecret"];
                        options.SaveTokens = true;
                        options.CallbackPath = "/signin/github";
                        options.Scope.Add("user:email");
                        options.Scope.Add("read:org");
                        options.Events = new OAuthEvents {OnCreatingTicket = AddOrganizationRoles};
                    })
                .AddPersonalAccessToken<ApplicationUser>(
                    options =>
                    {
                        options.Events = new PersonalAccessTokenEvents<ApplicationUser>
                        {
                            NewToken = async (context, user, name, hash) =>
                            {
                                var dbContext = context.RequestServices.GetRequiredService<BuildAssetRegistryContext>();
                                int userId = user.Id;
                                var token = new ApplicationUserPersonalAccessToken
                                {
                                    ApplicationUserId = userId,
                                    Name = name,
                                    Hash = hash,
                                    Created = DateTimeOffset.UtcNow
                                };
                                await dbContext.Set<ApplicationUserPersonalAccessToken>().AddAsync(token);
                                await dbContext.SaveChangesAsync();

                                return token.Id;
                            },
                            GetTokenHash = async (context, tokenId) =>
                            {
                                var dbContext = context.RequestServices.GetRequiredService<BuildAssetRegistryContext>();
                                ApplicationUserPersonalAccessToken token = await dbContext
                                    .Set<ApplicationUserPersonalAccessToken>()
                                    .Where(t => t.Id == tokenId)
                                    .Include(t => t.ApplicationUser)
                                    .FirstOrDefaultAsync();
                                if (token == null)
                                {
                                    return null;
                                }

                                return (token.Hash, token.ApplicationUser);
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
                            var userManager = ctx.HttpContext.RequestServices
                                .GetRequiredService<UserManager<ApplicationUser>>();
                            var signInManager = ctx.HttpContext.RequestServices
                                .GetRequiredService<SignInManager<ApplicationUser>>();


                            // extract the userId from the ClaimsPrincipal and read the user from the Db
                            ApplicationUser user = await userManager.GetUserAsync(ctx.Principal);
                            if (user == null)
                            {
                                ctx.Principal = null;
                            }
                            else
                            {
                                ctx.Principal = await signInManager.CreateUserPrincipalAsync(user);
                            }
                        }
                    };
                });
        }

        private static async Task AddOrganizationRoles(OAuthCreatingTicketContext context)
        {
            var client =
                new GitHubClient(new ProductHeaderValue(ProductName, ProductVersion))
                {
                    Credentials = new Credentials(context.AccessToken)
                };
            IEnumerable<string> orgs = (await client.Organization.GetAllForCurrent()).Select(org => org.Login);
            foreach (string org in orgs)
            {
                context.Identity.AddClaim(
                    new Claim(ClaimTypes.Role, $"github:org:{org}", ClaimValueTypes.String, GitHubScheme));
            }
        }
    }
}
