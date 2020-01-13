// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.Github.IssueLabeler.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var diffHelper = new DiffHelper();
            var datasetHelper = new DatasetHelper(diffHelper);
            var labeler = new Labeler(
                    Configuration["GitHubRepoOwner"],
                    Configuration["GitHubRepoName"],
                    Configuration["SecretUri"],
                    double.Parse(Configuration["Threshold"]), diffHelper, datasetHelper);
            services.AddMvc();

            services.AddSingleton(labeler)
            .AddSingleton(datasetHelper)
            .AddSingleton(diffHelper);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
