using Maestro.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.DotNet.ServiceFabric.ServiceHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Maestro.Web
{
    public class ApplicationUser : IdentityUser
    {
    }

    public class ApplicationRole
    {
        public string Name { get; }

        public ApplicationRole(string name)
        {
            Name = name;
        }
    }

    public partial class Startup
    {
        public const string GitHubScheme = "GitHub";

        private void ConfigureAuthServices(IServiceCollection services)
        {
            if (ServiceContext.IsServiceFabric)
            {
                services.AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                }).AddEntityFrameworkStores<BuildAssetRegistryContext>();

                services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, ConfigureApplicationCookieAuthentication>();
                services.AddSingleton<IConfigureOptions<GitHubAuthenticationOptions>, ConfigureGitHubAuthentication>();
                services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtUserStore>();
                services.AddSingleton<IAuthenticationSchemeProvider, ContextAwareAuthenticationSchemeProvider>();
                services.AddAuthentication()
                    .AddOAuth<GitHubAuthenticationOptions, GitHubAuthenticationHandler>(GitHubScheme, options => { })
                    .AddJwtBearer(options => { });

                services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            }

            services.Configure<MvcOptions>(options =>
            {
                options.Conventions.Add(new DefaultAuthorizeActionModelConvention("github:org:Microsoft"));
            });
        }
    }

    public class GitHubAuthenticationOptions : OAuthOptions
    {
        public GitHubAuthenticationOptions()
        {
            AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
            TokenEndpoint = "https://github.com/login/oauth/access_token";
            UserInformationEndpoint = "https://api.github.com/user";
        }
    }

    internal class ConfigureApplicationCookieAuthentication : IConfigureNamedOptions<CookieAuthenticationOptions>
    {
        public IServiceProvider Provider { get; }

        public ConfigureApplicationCookieAuthentication(IServiceProvider provider)
        {
            Provider = provider;
        }

        public void Configure(CookieAuthenticationOptions options) => Configure(Options.DefaultName, options);

        public void Configure(string name, CookieAuthenticationOptions options)
        {
            if (name != IdentityConstants.ExternalScheme)
            {
                return;
            }
            options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
            options.ReturnUrlParameter = "returnUrl";
            options.LoginPath = "/Account/GitHubLogIn";
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = 401;
                        return Task.FromResult(true);
                    }
                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.FromResult(true);
                },
                OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = 403;
                    return Task.FromResult(true);
                }
            };
        }
    }

    internal class ConfigureGitHubAuthentication : IConfigureNamedOptions<GitHubAuthenticationOptions>
    {
        public IServiceConfig Config { get; }

        public ConfigureGitHubAuthentication(IServiceConfig config)
        {
            Config = config;
        }

        public void Configure(string name, GitHubAuthenticationOptions options)
        {
            if (name != Startup.GitHubScheme)
            {
                return;
            }
            options.ClientId = Config["GitHubConfig"]["ClientId"];
            options.ClientSecret = Config["GitHubConfig"]["ClientSecret"];
            options.SaveTokens = true;
            options.CallbackPath = "/signin/github";
            options.Scope.Add("user:email");
            options.Scope.Add("read:org");
            options.Events = new OAuthEvents
            {
                OnCreatingTicket = AddOrganizationRoles
            };
        }

        private static async Task AddOrganizationRoles(OAuthCreatingTicketContext context)
        {
            var client = new GitHubClient(new ProductHeaderValue("dotnet-repro-tool", "1.0"))
            {
                Credentials = new Credentials(context.AccessToken)
            };
            IEnumerable<string> orgs = (await client.Organization.GetAllForCurrent()).Select(org => org.Login);
            foreach (string org in orgs)
            {
                context.Identity.AddClaim(new Claim(ClaimTypes.Role, $"github:org:{org}", ClaimValueTypes.String, Startup.GitHubScheme));
            }
        }

        public void Configure(GitHubAuthenticationOptions options) => Configure(Options.DefaultName, options);
    }

    internal class DefaultAuthorizeActionModelConvention : IActionModelConvention
    {
        public DefaultAuthorizeActionModelConvention(string requiredRole)
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(requiredRole)
                .Build();
            Filter = new AuthorizeFilter(policy);
        }

        public AuthorizeFilter Filter { get; }

        public void Apply(ActionModel action)
        {
            var preexisting = action.Controller.Filters.Concat(action.Filters);
            if (preexisting.Any(f => f is IAsyncAuthorizationFilter || f is IAllowAnonymousFilter))
                return;
            var attributes = action.Controller.Attributes.Concat(action.Attributes);
            if (attributes.Any(a => a is IAllowAnonymous || a is IAuthorizeData))
                return;
            action.Filters.Add(Filter);
        }
    }

    public class ConfigureJwtUserStore : IConfigureNamedOptions<JwtBearerOptions>
    {
        private ISecretService SecretService { get; }
        private IServiceContext ServiceContext { get; }
        public SignInManager<ApplicationUser> SignInManager { get; }
        public UserManager<ApplicationUser> UserManager { get; }

        public void Configure(string name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme)
            {
                return;
            }
            IServiceConfig config = ServiceContext.Config;
            string issuer = config["JwtConfig"]?["Issuer"];
            string issuerSigningKey = SecretService.GetValueAsync("JwtTokenSigningKey").GetAwaiter().GetResult();

            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Convert.FromBase64String(issuerSigningKey)
                    ),
                ValidAudience = issuer,
                ValidIssuer = issuer,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    // The JWT doesn't have anything but NameIdentifier to make it smaller, grab the rest from the storage
                    var user = await UserManager.GetUserAsync(ctx.Principal);
                    if (user == null)
                    {
                        ctx.Principal = null;
                        ctx.Fail("Invalid Token");
                    }
                    else
                    {
                        ctx.Principal = await SignInManager.CreateUserPrincipalAsync(user);
                        ctx.Success();
                    }
                }
            };
        }

        public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
    }
}
