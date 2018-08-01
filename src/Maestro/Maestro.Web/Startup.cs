// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Autofac;
using FluentValidation.AspNetCore;
using Maestro.Web.Data;
using Microsoft.AspNetCore.ApiVersioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Maestro.Web
{
    public partial class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            HostingEnvironment = env;
            Configuration = GetConfiguration(env);
        }

        public IHostingEnvironment HostingEnvironment { get; set; }
        public IConfigurationRoot Configuration { get; }

        public static KeyVaultClient GetKeyVaultClient(string connectionString)
        {
            var provider = new AzureServiceTokenProvider(connectionString);
            return new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(provider.KeyVaultTokenCallback));
        }

        public static IConfigurationRoot GetConfiguration(IHostingEnvironment env)
        {
            IConfigurationRoot bootstrapConfig = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile(".config/settings.json")
                .AddJsonFile($".config/settings.{env.EnvironmentName}.json")
                .Build();

            Func<KeyVaultClient> clientFactory;
            if (env.IsDevelopment() && Program.RunningInServiceFabric())
            {
                var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
                string appId = "388be541-91ed-4771-8473-5791e071ed14";
                string certThumbprint = "C4DFDCC47D95C1C64B55B42946CCEFDDF9E46FAB";

                string connectionString = $"RunAs=App;AppId={appId};TenantId={tenantId};CertificateThumbprint={certThumbprint};CertificateStoreLocation=LocalMachine";
                clientFactory = () => GetKeyVaultClient(connectionString);
            }
            else
            {
                clientFactory = () => GetKeyVaultClient(null);
            }

            string keyVaultUri = bootstrapConfig["KeyVaultUri"];


            return new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
                .AddKeyVaultMappedJsonFile(".config/settings.json", keyVaultUri, clientFactory)
                .AddKeyVaultMappedJsonFile($".config/settings.{env.EnvironmentName}.json", keyVaultUri, clientFactory)
                .Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if (HostingEnvironment.IsDevelopment())
            {
                services.AddDataProtection();
            }
            else
            {
                IConfigurationSection dpConfig = Configuration.GetSection("DataProtection");
                services.AddDataProtection()
                    .PersistKeysToAzureBlobStorage(new Uri(dpConfig["KeyFileUri"]))
                    .ProtectKeysWithAzureKeyVault(GetKeyVaultClient(null), dpConfig["KeyIdentifier"]);
            }

            ConfigureApiServices(services);

            services.Configure<CookiePolicyOptions>(
                options =>
                {
                    options.CheckConsentNeeded = context => true;
                    options.MinimumSameSitePolicy = SameSiteMode.None;
                });

            services.AddDbContext<BuildAssetRegistryContext>(
                options =>
                {
                    options.UseSqlServer(Configuration.GetSection("BuildAssetRegistry")["ConnectionString"]);
                });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddFluentValidation(options => options.RegisterValidatorsFromAssemblyContaining<Startup>())
                .AddRazorPagesOptions(
                    options =>
                    {
                        options.Conventions.AuthorizeFolder("/");
                        options.Conventions.AllowAnonymousToPage("/Index");
                    });

            ConfigureAuthServices(services);
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
        }

        private void ConfigureApiExceptions(IApplicationBuilder app)
        {
            app.Run(
                async ctx =>
                {
                    var result = new ApiError($"An error occured.");
                    MvcJsonOptions jsonOptions =
                        ctx.RequestServices.GetRequiredService<IOptions<MvcJsonOptions>>().Value;
                    string output = JsonConvert.SerializeObject(result, jsonOptions.SerializerSettings);
                    ctx.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                    await ctx.Response.WriteAsync(output, Encoding.UTF8);
                });
        }

        private void ConfigureApi(IApplicationBuilder app)
        {
            app.UseExceptionHandler(ConfigureApiExceptions);

            app.UseAuthentication();
            app.UseMvc();

            app.Use(
                (ctx, next) =>
                {
                    if (ctx.Request.Path == "/api/swagger.json")
                    {
                        var vcp = ctx.RequestServices.GetRequiredService<VersionedControllerProvider>();
                        string highestVersion = vcp.Versions.Keys.OrderByDescending(n => n).First();
                        ctx.Request.Path = $"/api/{highestVersion}/swagger.json";
                    }

                    return next();
                });

            app.UseSwagger(
                options =>
                {
                    options.RouteTemplate = "api/{documentName}/swagger.json";
                    options.PreSerializeFilters.Add(
                        (doc, req) =>
                        {
                            doc.Host = req.Host.Value;
                            if (HostingEnvironment.IsDevelopment() && !Program.RunningInServiceFabric())
                            {
                                doc.Schemes = new List<string> {"http"};
                            }
                            else
                            {
                                doc.Schemes = new List<string> {"https"};
                            }

                            req.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";
                        });
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.MapWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), ConfigureApi);

            app.UseRewriter(new RewriteOptions().AddRedirect("^swagger(/ui)?/?$", "/swagger/ui/index.html"));
            app.UseStatusCodePages();
            app.UseCookiePolicy();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}
