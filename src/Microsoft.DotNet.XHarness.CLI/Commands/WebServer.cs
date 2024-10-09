// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Microsoft.DotNet.XHarness.CLI.Commands;

public class WebServer
{
    internal static Task<ServerURLs> Start(IWebServerArguments arguments, ILogger logger, CancellationToken token, Func<WebSocket, Task>? onConsoleConnected = null)
    {
        var options = TestWebServerOptions.FromArguments(arguments);
        options.OnConsoleConnected = onConsoleConnected;
        return Start(options, logger, token);
    }

    internal static async Task<ServerURLs> Start(TestWebServerOptions webServerOptions, ILogger logger, CancellationToken token)
    {
        var urls = webServerOptions.UseHttps
                ? new string[] { "http://127.0.0.1:0", "https://127.0.0.1:0" }
                : new string[] { "http://127.0.0.1:0" };

        var builder = new WebHostBuilder()
            .UseKestrel()
            .UseStartup<TestWebServerStartup>()
            .ConfigureLogging(logging =>
            {
                logging.AddConsole().AddFilter(null, LogLevel.Warning);
            })
            .ConfigureServices((ctx, services) =>
            {
                if (webServerOptions.UseCors)
                {
                    services.AddCors(o => o.AddPolicy("AnyCors", builder =>
                        {
                            builder.AllowAnyOrigin()
                                .AllowAnyMethod()
                                .AllowAnyHeader()
                                .WithExposedHeaders("*");
                        }));
                }
                services.AddRouting();
                services.AddSingleton<ILogger>(logger);
                services.Configure<TestWebServerOptions>(ctx.Configuration);
                services.Configure<TestWebServerOptions>(options =>
                {
                    webServerOptions.CopyTo(options);
                });
            })
            .UseUrls(urls);

        if (webServerOptions.ContentRoot != null)
        {
            builder.UseContentRoot(webServerOptions.ContentRoot);
        }

        var host = builder.Build();

        await host.StartAsync(token);

        var ipAddress = host.ServerFeatures
            .Get<IServerAddressesFeature>()?
            .Addresses
            .Where(a => a.StartsWith("http:"))
            .Select(a => new Uri(a))
            .Select(uri => $"{uri.Host}:{uri.Port}")
            .FirstOrDefault();

        var ipAddressSecure = webServerOptions.UseHttps
            ? host.ServerFeatures
                .Get<IServerAddressesFeature>()?
                .Addresses
                .Where(a => a.StartsWith("https:"))
                .Select(a => new Uri(a))
                .Select(uri => $"{uri.Host}:{uri.Port}")
                .FirstOrDefault()
            : null;

        if (ipAddress == null || (webServerOptions.UseHttps && ipAddressSecure == null))
        {
            throw new InvalidOperationException("Failed to determine web server's IP address or port");
        }

        return new ServerURLs(ipAddress, ipAddressSecure);
    }

    private class TestWebServerStartup
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger _logger;

        public TestWebServerStartup(IWebHostEnvironment hostingEnvironment, ILogger logger)
        {
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
        }

        public void Configure(IApplicationBuilder app, IOptionsMonitor<TestWebServerOptions> optionsAccessor)
        {
            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".wasm"] = "application/wasm";
            provider.Mappings[".cjs"] = "text/javascript";
            provider.Mappings[".mjs"] = "text/javascript";

            foreach (var extn in new string[] { ".dll", ".pdb", ".dat", ".blat", ".webcil" })
            {
                provider.Mappings[extn] = "application/octet-stream";
            }

            var options = optionsAccessor.CurrentValue;

            if (options.UseCrossOriginPolicy)
            {
                app.Use((context, next) =>
                {
                    context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
                    context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
                    return next();
                });
            }

            if (options.UseDefaultFiles)
            {
                app.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = new PhysicalFileProvider(_hostingEnvironment.ContentRootPath)
                });
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(_hostingEnvironment.ContentRootPath),
                ContentTypeProvider = provider,
                ServeUnknownFileTypes = true
            });

            if (options.UseCors)
            {
                app.UseCors("AnyCors");
            }
            app.UseWebSockets();
            if (options.OnConsoleConnected != null)
            {
                app.UseRouter(router =>
                {
                    router.MapGet("/console", async context =>
                    {
                        if (!context.WebSockets.IsWebSocketRequest)
                        {
                            context.Response.StatusCode = 400;
                            return;
                        }

                        var socket = await context.WebSockets.AcceptWebSocketAsync();
                        await options.OnConsoleConnected(socket);
                    });
                });
            }

            foreach (var middleware in options.EchoServerMiddlewares)
            {
                app.UseMiddleware(middleware);
                _logger.LogInformation($"Loaded {middleware.FullName} middleware");
            }
        }
    }

    internal class TestWebServerOptions
    {
        public Func<WebSocket, Task>? OnConsoleConnected { get; set; }
        public IList<Type> EchoServerMiddlewares { get; set; } = new List<Type>();
        public bool UseCors { get; set; }
        public bool UseHttps { get; set; }
        public bool UseCrossOriginPolicy { get; set; }
        public bool UseDefaultFiles { get; set; }
        public string? ContentRoot { get; set; }

        public void CopyTo(TestWebServerOptions otherOptions)
        {
            otherOptions.OnConsoleConnected = OnConsoleConnected;
            otherOptions.EchoServerMiddlewares = EchoServerMiddlewares;
            otherOptions.UseCors = UseCors;
            otherOptions.UseHttps = UseHttps;
            otherOptions.UseCrossOriginPolicy = UseCrossOriginPolicy;
            otherOptions.UseDefaultFiles = UseDefaultFiles;
            otherOptions.ContentRoot = ContentRoot;
        }

        public static TestWebServerOptions FromArguments(IWebServerArguments arguments)
        {
            TestWebServerOptions options = new();
            options.UseCors = arguments.WebServerUseCors;
            options.UseHttps = arguments.WebServerUseHttps;
            options.UseCrossOriginPolicy = arguments.WebServerUseCrossOriginPolicy;
            options.UseDefaultFiles = arguments.WebServerUseDefaultFiles;
            foreach (var middlewareType in arguments.WebServerMiddlewarePathsAndTypes.GetLoadedTypes())
            {
                options.EchoServerMiddlewares.Add(middlewareType);
            }
            return options;
        }
    }
}

public record ServerURLs(string Http, string? Https);
