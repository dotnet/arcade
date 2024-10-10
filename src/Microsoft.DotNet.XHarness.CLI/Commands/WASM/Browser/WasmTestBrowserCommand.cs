// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Safari;
using SeleniumLogLevel = OpenQA.Selenium.LogLevel;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

internal class WasmTestBrowserCommand : XHarnessCommand<WasmTestBrowserCommandArguments>
{
    private const string CommandHelp = "Executes tests on WASM using a browser";

    protected override string CommandUsage { get; } = "wasm test-browser [OPTIONS] -- [BROWSER OPTIONS]";
    protected override string CommandDescription { get; } = CommandHelp;

    protected override WasmTestBrowserCommandArguments Arguments { get; } = new();

    public WasmTestBrowserCommand()
        : base(TargetPlatform.WASM, "test-browser", allowsExtraArgs: true, new ServiceCollection(), CommandHelp)
    {
    }

    protected override async Task<ExitCode> InvokeInternal(ILogger logger)
    {
        var xmlResultsFilePath = Path.Combine(Arguments.OutputDirectory, "testResults.xml");
        File.Delete(xmlResultsFilePath);

        var stdoutFilePath = Path.Combine(Arguments.OutputDirectory, "wasm-console.log");
        File.Delete(stdoutFilePath);

        var symbolicator = WasmSymbolicatorBase.Create(Arguments.SymbolicatorArgument.GetLoadedTypes().FirstOrDefault(),
                                                       Arguments.SymbolMapFileArgument,
                                                       Arguments.SymbolicatePatternsFileArgument,
                                                       logger);

        var serviceProvider = Services.BuildServiceProvider();
        var diagnosticsData = serviceProvider.GetRequiredService<IDiagnosticsData>();

        var logProcessor = new WasmTestMessagesProcessor(xmlResultsFilePath,
                                                         stdoutFilePath,
                                                         logger,
                                                         Arguments.ErrorPatternsFile,
                                                         symbolicator);
        var runner = new WasmBrowserTestRunner(
                            Arguments,
                            PassThroughArguments,
                            logProcessor,
                            logger);

        diagnosticsData.Target = Arguments.Browser.Value.ToString();
        (DriverService driverService, IWebDriver driver) = Arguments.Browser.Value switch
        {
            Browser.Chrome => GetChromeDriver(Arguments.Locale, logger),
            Browser.Safari => GetSafariDriver(logger),
            Browser.Firefox => GetFirefoxDriver(logger),
            Browser.Edge => GetEdgeDriver(Arguments.Locale, logger),

            // shouldn't reach here
            _ => throw new ArgumentException($"Unknown browser : {Arguments.Browser}")
        };

        try
        {
            var exitCode = await runner.RunTestsWithWebDriver(driverService, driver);
            if ((int)exitCode != Arguments.ExpectedExitCode)
            {
                logger.LogError($"Application has finished with exit code {exitCode} but {Arguments.ExpectedExitCode} was expected");
                return ExitCode.GENERAL_FAILURE;
            }

            if (logProcessor.LineThatMatchedErrorPattern != null)
            {
                logger.LogError("Application exited with the expected exit code: {exitCode}."
                                + $" But found a line matching an error pattern: {logProcessor.LineThatMatchedErrorPattern}");
                return ExitCode.APP_CRASH;
            }

            return ExitCode.SUCCESS;
        }
        finally
        {
            if (Arguments.NoQuit)
            {
                logger.LogInformation("Tests are done. Press Ctrl+C to exit");
                var token = new CancellationToken(false);
                token.WaitHandle.WaitOne();
            }

            // close all tabs before quit is a workaround for broken Selenium - GeckoDriver communication in Firefox
            // https://github.com/dotnet/runtime/issues/101617
            var cts = new CancellationTokenSource();
            cts.CancelAfter(10000);
            try
            {
                logger.LogInformation($"Closing {driver.WindowHandles.Count} browser tabs before setting the main tab to config page and quitting.");
                while (driver.WindowHandles.Count > 1 && driverService.IsRunning)
                {
                    if (cts.IsCancellationRequested)
                    {
                        logger.LogInformation($"Timeout while trying to close tabs, {driver.WindowHandles.Count} is left open before quitting.");
                        break;
                    }
                    driver.Navigate().GoToUrl("about:config");
                    driver.Navigate().GoToUrl("about:blank");
                    driver.Close(); //Close Tab

                    var lastWindowHandle = driver.WindowHandles.LastOrDefault();
                    if (lastWindowHandle != null)
                    {
                        driver.SwitchTo().Window(lastWindowHandle);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                if (driverService.IsRunning)
                {
                    if (!cts.IsCancellationRequested && driver.WindowHandles.Count != 0)
                    {
                        driver.Navigate().GoToUrl("about:config");
                        driver.Navigate().GoToUrl("about:blank");
                    }
                    driver.Quit(); // Firefox driver hangs if Quit is not issued.
                    driver.Dispose();
                    driverService.Dispose();
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Error while closing browser: {e}");
            }
        }
    }

    private (DriverService, IWebDriver) GetSafariDriver(ILogger logger)
    {
        var options = new SafariOptions();
        options.SetLoggingPreference(LogType.Browser, SeleniumLogLevel.All);

        logger.LogInformation("Starting Safari");

        return CreateWebDriver(
                    () => SafariDriverService.CreateDefaultService(),
                    driverService => new SafariDriver(driverService, options, Arguments.Timeout));
    }

    private (DriverService, IWebDriver) GetFirefoxDriver(ILogger logger)
    {
        var options = new FirefoxOptions();
        options.SetLoggingPreference(LogType.Browser, SeleniumLogLevel.All);

        if (!string.IsNullOrEmpty(Arguments.BrowserLocation))
        {
            options.BrowserExecutableLocation = Arguments.BrowserLocation;
            logger.LogInformation($"Using Firefox from {Arguments.BrowserLocation}");
        }

        options.AddArguments(Arguments.BrowserArgs.Value);
        if (!Arguments.NoHeadless)
            options.AddArguments("--headless");

        if (!Arguments.NoIncognito)
            options.AddArguments("-private-window");

        options.PageLoadStrategy = Arguments.PageLoadStrategy.Value;

        logger.LogInformation($"Starting Firefox with args: {string.Join(' ', options.ToCapabilities())} and load strategy: {Arguments.PageLoadStrategy.Value}");

        return CreateWebDriver(
                    () => FirefoxDriverService.CreateDefaultService(),
                    (driverService) => new FirefoxDriver(driverService, options, Arguments.Timeout));
    }

    private (DriverService, IWebDriver) GetChromeDriver(string sessionLanguage, ILogger logger)
        => GetChromiumDriver<ChromeOptions, ChromeDriver, ChromeDriverService>(
                    "chromedriver",
                    sessionLanguage,
                    options => ChromeDriverService.CreateDefaultService(),
                    logger);

    private (DriverService, IWebDriver) GetEdgeDriver(string sessionLanguage, ILogger logger)
        => GetChromiumDriver<EdgeOptions, EdgeDriver, EdgeDriverService>(
                    "edgedriver",
                    sessionLanguage,
                    options =>
                    {
                        options.UseChromium = true;
                        return EdgeDriverService.CreateDefaultServiceFromOptions(options);
                    }, logger);

    private (DriverService, IWebDriver) GetChromiumDriver<TDriverOptions, TDriver, TDriverService>(
        string driverName, string sessionLanguage, Func<TDriverOptions, TDriverService> getDriverService, ILogger logger)
        where TDriver : ChromiumDriver
        where TDriverOptions : ChromiumOptions
        where TDriverService : ChromiumDriverService
    {
        var options = Activator.CreateInstance<TDriverOptions>();
        options.SetLoggingPreference(LogType.Browser, SeleniumLogLevel.All);

        if (!string.IsNullOrEmpty(Arguments.BrowserLocation))
        {
            options.BinaryLocation = Arguments.BrowserLocation;
            logger.LogInformation($"Using Chrome from {Arguments.BrowserLocation}");
        }

        options.AddArguments(Arguments.BrowserArgs.Value);

        if (!Arguments.NoHeadless && !Arguments.BackgroundThrottling)
            options.AddArguments("--headless");

        if (Arguments.DebuggerPort.Value != null)
            options.AddArguments($"--remote-debugging-port={Arguments.DebuggerPort}");

        if (!Arguments.NoIncognito)
            options.AddArguments("--incognito");

        if (!Arguments.BackgroundThrottling)
        {
            options.AddArguments(new[]
            {
                    "--disable-background-timer-throttling",
                    "--disable-backgrounding-occluded-windows",
                    "--disable-renderer-backgrounding",
                    "--enable-features=NetworkService,NetworkServiceInProcess",
                });
        }
        else
        {
            options.AddArguments(@"--enable-features=IntensiveWakeUpThrottling:grace_period_seconds/1");
        }

        options.AddArguments(new[]
        {
            // added based on https://github.com/puppeteer/puppeteer/blob/main/src/node/Launcher.ts#L159-L181
            "--allow-insecure-localhost",
            "--disable-breakpad",
            "--disable-component-extensions-with-background-pages",
            "--disable-dev-shm-usage",
            "--disable-extensions",
            "--disable-features=TranslateUI",
            "--disable-ipc-flooding-protection",
            "--force-color-profile=srgb",
            "--metrics-recording-only"
        });

        if (File.Exists("/.dockerenv"))
        {
            // Use --no-sandbox for containers, and codespaces
            options.AddArguments("--no-sandbox");
        }

        if (Arguments.NoQuit)
            options.LeaveBrowserRunning = true;

        if (options is ChromeOptions chromeOptions)
            chromeOptions.PageLoadStrategy = Arguments.PageLoadStrategy.Value;
        if (options is EdgeOptions edgeOptions)
            edgeOptions.PageLoadStrategy = Arguments.PageLoadStrategy.Value;

        logger.LogInformation($"Starting {driverName} with args: {string.Join(' ', options.Arguments)} and load strategy: {Arguments.PageLoadStrategy.Value}");

        // We want to explicitly specify a timeout here. This is for for the
        // driver commands, like getLog. The default is 60s, which ends up
        // timing out when getLog() is waiting, and doesn't receive anything
        // for 60s.
        //
        // Since, we almost all the output gets written via the websocket now,
        // getLog() might not see anything for long durations!
        //
        // So -> use a larger timeout!

        string[] err_snippets = new[]
        {
                "exited abnormally",
                "Cannot start the driver service",
                "failed to start"
            };

        foreach (var file in Directory.EnumerateFiles(Arguments.OutputDirectory, $"{driverName}-*.log"))
            File.Delete(file);

        int max_retries = 3;
        int retry_num = 0;
        while (true)
        {
            TDriverService? driverService = null;
            try
            {
                driverService = getDriverService(options);
                driverService.DriverProcessStarting += (object? sender, DriverProcessStartingEventArgs e) =>
                {
                    // Browser respects LANGUAGE in the first place, only if empty it checks LANG
                    e.DriverServiceProcessStartInfo.EnvironmentVariables["LANGUAGE"] = sessionLanguage;
                };

                driverService.EnableAppendLog = false;
                driverService.EnableVerboseLogging = true;
                driverService.LogPath = Path.Combine(Arguments.OutputDirectory, $"{driverName}-{retry_num}.log");

                if (Activator.CreateInstance(typeof(TDriver), driverService, options, Arguments.Timeout.Value) is not TDriver driver)
                {
                    throw new ArgumentException($"Failed to create instance of {typeof(TDriver)}");
                }

                return (driverService, driver);
            }
            catch (TargetInvocationException tie) when
                        (tie.InnerException is WebDriverException wde
                            && err_snippets.Any(s => wde.ToString().Contains(s)) && retry_num < max_retries - 1)
            {
                // chrome can sometimes crash on startup when launching from chromedriver.
                // As a *workaround*, let's retry that a few times
                // Example error seen:
                //     [12:41:07] crit: OpenQA.Selenium.WebDriverException: unknown error: Chrome failed to start: exited abnormally.
                //    (chrome not reachable)

                // Log on max-1 tries, and rethrow on the last one
                logger.LogWarning($"Failed to start the browser, attempt #{retry_num}: {wde}");

                driverService?.Dispose();
            }
            catch
            {
                driverService?.Dispose();
                throw;
            }

            retry_num++;
        }
    }

    private static (DriverService, IWebDriver) CreateWebDriver<TDriverService>(Func<TDriverService> getDriverService, Func<TDriverService, IWebDriver> getDriver)
        where TDriverService : DriverService
    {
        var driverService = getDriverService();
        try
        {
            return (driverService, getDriver(driverService));
        }
        catch
        {
            driverService?.Dispose();
            throw;
        }
    }
}
