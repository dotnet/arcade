// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Configuration;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.RecursiveSigning.Cli
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // ── Option definitions ─────────────────────────────────────────

            var configOption = new Option<string>("--config")
            {
                Description = "Path to the signing rules JSON config file",
                Required = true
            };

            var inputOption = new Option<string[]>("--input")
            {
                Description = "Top-level files or glob patterns to process (repeatable)",
                Required = true,
                AllowMultipleArgumentsPerToken = true
            };

            var tempOption = new Option<string>("--temp")
            {
                Description = "Temporary working directory",
                DefaultValueFactory = _ => Path.Combine(Path.GetTempPath(), "recursive-signing-cli")
            };

            var outputOption = new Option<string?>("--output")
            {
                Description = "Output directory for final root artifacts"
            };

            var verboseOption = new Option<bool>("--verbose")
            {
                Description = "Enable debug logging"
            };

            var logDirOption = new Option<string?>("--log-dir")
            {
                Description = "Directory for ESRP CLI invocation logs (default: <temp>/esrp-logs)"
            };

            var esrpOption = new Option<bool>("--esrp")
            {
                Description = "Use ESRP CLI signing provider instead of dry-run"
            };

            var dryRunOption = new Option<bool>("--dry-run")
            {
                Description = "Print ESRP submission JSON without invoking CLI (requires --esrp)"
            };

            var esrpCliPathOption = new Option<string?>("--esrp-cli-path")
            {
                Description = "Path to esrpcli.dll (default: bundled copy; requires --esrp)"
            };

            var rootOption = new Option<string?>("--root")
            {
                Description = "Root directory for relative file paths (requires --esrp)"
            };

            var esrpClientIdOption = new Option<string?>("--esrp-client-id")
            {
                Description = "ESRP client identifier that identifies the signing account to the ESRP service"
            };

            var esrpAppRegistrationOption = new Option<string?>("--esrp-app-registration")
            {
                Description = "Entra ID app registration client ID used to authenticate to the ESRP service"
            };

            var esrpTenantIdOption = new Option<string?>("--esrp-tenant-id")
            {
                Description = "Entra ID tenant ID for ESRP authentication"
            };

            var esrpKeyVaultNameOption = new Option<string?>("--esrp-keyvault-name")
            {
                Description = "Key vault name for ESRP request-signing certificate"
            };

            var esrpCertNameOption = new Option<string?>("--esrp-cert-name")
            {
                Description = "Certificate name in the key vault"
            };

            var federatedTokenOption = new Option<bool>("--federated-token")
            {
                Description = "Use federated token auth mode to authenticate to the ESRP service (default: certificate auth)"
            };

            var serviceConnectionIdOption = new Option<string?>("--service-connection-id")
            {
                Description = "Azure DevOps service connection GUID for federated token auth"
            };

            // ── Root command ───────────────────────────────────────────────

            var rootCommand = new RootCommand("Recursive code signing CLI tool using ESRP")
            {
                TreatUnmatchedTokensAsErrors = true
            };

            rootCommand.Options.Add(configOption);
            rootCommand.Options.Add(inputOption);
            rootCommand.Options.Add(tempOption);
            rootCommand.Options.Add(outputOption);
            rootCommand.Options.Add(verboseOption);
            rootCommand.Options.Add(logDirOption);
            rootCommand.Options.Add(esrpOption);
            rootCommand.Options.Add(dryRunOption);
            rootCommand.Options.Add(esrpCliPathOption);
            rootCommand.Options.Add(rootOption);
            rootCommand.Options.Add(esrpClientIdOption);
            rootCommand.Options.Add(esrpAppRegistrationOption);
            rootCommand.Options.Add(esrpTenantIdOption);
            rootCommand.Options.Add(esrpKeyVaultNameOption);
            rootCommand.Options.Add(esrpCertNameOption);
            rootCommand.Options.Add(federatedTokenOption);
            rootCommand.Options.Add(serviceConnectionIdOption);

            rootCommand.SetAction(async (result, cancellationToken) =>
            {
                var configPath = result.GetValue(configOption)!;
                var inputPatterns = result.GetValue(inputOption) ?? Array.Empty<string>();
                var tempDirectory = result.GetValue(tempOption)!;
                var outputDirectory = result.GetValue(outputOption);
                var verbose = result.GetValue(verboseOption);
                var logDir = result.GetValue(logDirOption);
                var useESRP = result.GetValue(esrpOption);
                var dryRun = result.GetValue(dryRunOption);
                var esrpCliPath = result.GetValue(esrpCliPathOption);
                var rootDirectory = result.GetValue(rootOption);
                var esrpClientId = result.GetValue(esrpClientIdOption);
                var esrpAppRegistration = result.GetValue(esrpAppRegistrationOption);
                var esrpTenantId = result.GetValue(esrpTenantIdOption);
                var esrpKeyVaultName = result.GetValue(esrpKeyVaultNameOption);
                var esrpCertName = result.GetValue(esrpCertNameOption);
                var useFederatedToken = result.GetValue(federatedTokenOption);
                var serviceConnectionId = result.GetValue(serviceConnectionIdOption);

                return await RunAsync(
                    configPath, inputPatterns, tempDirectory, outputDirectory, verbose, logDir,
                    useESRP, dryRun, esrpCliPath, rootDirectory,
                    esrpClientId, esrpAppRegistration, esrpTenantId, esrpKeyVaultName, esrpCertName,
                    useFederatedToken, serviceConnectionId);
            });

            return await rootCommand.Parse(args).InvokeAsync();
        }

        private static async Task<int> RunAsync(
            string configPath,
            string[] inputPatterns,
            string tempDirectory,
            string? outputDirectory,
            bool verbose,
            string? logDir,
            bool useESRP,
            bool dryRun,
            string? esrpCliPath,
            string? rootDirectory,
            string? esrpClientId,
            string? esrpAppRegistration,
            string? esrpTenantId,
            string? esrpKeyVaultName,
            string? esrpCertName,
            bool useFederatedToken,
            string? serviceConnectionId)
        {
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"Error: Config file not found: {configPath}");
                return 1;
            }

            DefaultCertificateRules rules;
            try
            {
                var reader = new DefaultCertificateRulesReader();
                rules = reader.ReadFromFile(configPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Failed to read config file '{configPath}': {ex.Message}");
                return 1;
            }

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information));
            services.AddRecursiveSigning();
            services.AddContainerHandler<ZipContainerHandler>();
            services.AddSingleton<IFileAnalyzer, DefaultFileAnalyzer>();
            services.AddSingleton<ICertificateCalculator>(_ => new DefaultCertificateCalculator(rules));

            if (useESRP)
            {
                if (!dryRun)
                {
                    var missing = new List<string>();
                    if (string.IsNullOrEmpty(esrpClientId)) missing.Add("--esrp-client-id");
                    if (string.IsNullOrEmpty(esrpAppRegistration)) missing.Add("--esrp-app-registration");
                    if (string.IsNullOrEmpty(esrpTenantId)) missing.Add("--esrp-tenant-id");
                    if (string.IsNullOrEmpty(esrpKeyVaultName)) missing.Add("--esrp-keyvault-name");
                    if (string.IsNullOrEmpty(esrpCertName)) missing.Add("--esrp-cert-name");
                    if (useFederatedToken && string.IsNullOrEmpty(serviceConnectionId)) missing.Add("--service-connection-id");
                    if (missing.Count > 0)
                    {
                        Console.Error.WriteLine($"Error: The following required ESRP options are missing: {string.Join(", ", missing)}");
                        return 1;
                    }
                }

                var esrpConfig = new ESRPCliSigningConfiguration
                {
                    ESRPCliPath = esrpCliPath ?? GetBundledEsrpCliPath(),
                    TempDirectory = tempDirectory,
                    LogDirectory = logDir ?? Path.Combine(tempDirectory, "esrp-logs"),
                    RootDirectory = rootDirectory ?? Directory.GetCurrentDirectory(),
                    DryRun = dryRun,
                    VerboseLogging = verbose,
                    AuthMode = useFederatedToken ? ESRPAuthMode.FederatedToken : ESRPAuthMode.Certificate,
                    EsrpClientId = esrpClientId ?? "",
                    ClientId = esrpAppRegistration ?? "",
                    TenantId = esrpTenantId ?? "",
                    KeyVaultName = esrpKeyVaultName ?? "",
                    CertificateName = esrpCertName ?? "",
                    ServiceConnectionId = serviceConnectionId ?? "",
                    EncryptedAuthCertPath = Environment.GetEnvironmentVariable("ESRP_AUTH_CERT_PATH") ?? "",
                    EncryptionKeyPath = Environment.GetEnvironmentVariable("ESRP_ENCRYPTION_KEY_PATH") ?? "",
                };
                services.AddSingleton(esrpConfig);
                services.AddSingleton<IProcessRunner, DefaultProcessRunner>();
                services.AddSingleton<ISigningProvider, ESRPCliSigningProvider>();
            }
            else
            {
                services.AddSingleton<ISigningProvider, DryRunSigningProvider>();
            }

            using var provider = services.BuildServiceProvider();
            var recursiveSigning = provider.GetRequiredService<IRecursiveSigning>();

            var resolvedInputs = ExpandInputPaths(inputPatterns);
            if (resolvedInputs.Count == 0)
            {
                Console.Error.WriteLine("No input files were resolved from the provided --input arguments.");
                return 1;
            }

            var request = new SigningRequest(
                resolvedInputs.Select(path => new FileInfo(path)).ToArray(),
                new SigningConfiguration(tempDirectory, outputDirectory),
                new SigningOptions());

            var result = await recursiveSigning.SignAsync(request);

            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"Signed files: {result.SignedFiles.Count}");
            foreach (var signedFile in result.SignedFiles)
            {
                Console.WriteLine($"  {signedFile.FilePath} => {signedFile.Certificate}");
            }

            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"ERROR: {error.FilePath} :: {error.Message}");
            }

            PrintSummaryTable(result);

            return result.Success ? 0 : 1;
        }

        private static void PrintSummaryTable(SigningResult result)
        {
            var t = result.Telemetry;
            var w = 58;
            var line = new string('-', w);
            Console.WriteLine();
            Console.WriteLine("+" + line + "+");
            Console.WriteLine("|" + "Signing Summary".PadLeft(37).PadRight(w) + "|");
            Console.WriteLine("+" + line + "+");
            Console.WriteLine($"| {"Total files discovered",-30}{t.TotalFiles,8} {"",-18}|");
            Console.WriteLine($"| {"Files signed",-30}{t.FilesSigned,8} {"",-18}|");
            Console.WriteLine($"| {"Files skipped",-30}{t.FilesSkipped,8} {"",-18}|");
            Console.WriteLine($"| {"Duplicate files",-30}{t.DuplicateFiles,8} {"",-18}|");
            Console.WriteLine($"| {"Signing rounds",-30}{t.SigningRounds,8} {"",-18}|");
            Console.WriteLine("+" + line + "+");
            Console.WriteLine($"| {"Discovery (unpack+analyze)",-30}{FormatDuration(t.DiscoveryDuration),12} {"",-14}|");
            Console.WriteLine($"| {"Signing (all rounds)",-30}{FormatDuration(t.SigningDuration),12} {"",-14}|");
            Console.WriteLine($"| {"Finalization",-30}{FormatDuration(t.FinalizationDuration),12} {"",-14}|");
            Console.WriteLine($"| {"Total",-30}{FormatDuration(t.Duration),12} {"",-14}|");

            if (t.Rounds.Count > 0)
            {
                Console.WriteLine("+" + line + "+");
                Console.WriteLine($"| {"Round",7} {"Files",7} {"Containers",12} {"Sign Time",11} {"Repack Time",13} |");
                Console.WriteLine("+" + line + "+");
                foreach (var r in t.Rounds)
                {
                    Console.WriteLine($"| {r.RoundNumber,7} {r.FilesSigned,7} {r.ContainersRepacked,12} {FormatDuration(r.SigningDuration),11} {FormatDuration(r.RepackDuration),13} |");
                }
            }

            Console.WriteLine("+" + line + "+");
        }

        private static string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalMinutes >= 1)
            {
                return $"{ts.Minutes}m {ts.Seconds:D2}.{ts.Milliseconds / 100}s";
            }
            return $"{ts.TotalSeconds:F1}s";
        }

        private static IReadOnlyList<string> ExpandInputPaths(IReadOnlyList<string> inputPatterns)
        {
            var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var inputPattern in inputPatterns)
            {
                if (ContainsWildcard(inputPattern))
                {
                    foreach (var path in ExpandGlob(inputPattern))
                    {
                        resolved.Add(path);
                    }
                }
                else
                {
                    var fullPath = Path.GetFullPath(inputPattern);
                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException($"Input file not found: {inputPattern}", inputPattern);
                    }

                    resolved.Add(fullPath);
                }
            }

            return resolved.ToArray();
        }

        private static IEnumerable<string> ExpandGlob(string pattern)
        {
            var normalizedPattern = pattern.Replace('/', Path.DirectorySeparatorChar);
            var fullPattern = Path.GetFullPath(normalizedPattern);

            var root = FindFixedRoot(fullPattern);
            if (!Directory.Exists(root))
            {
                yield break;
            }

            var relativePattern = fullPattern.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
            var regex = new Regex("^" + WildcardPatternToRegex(relativePattern) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            foreach (var candidate in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var relativeCandidate = candidate.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
                if (regex.IsMatch(relativeCandidate))
                {
                    yield return candidate;
                }
            }
        }

        private static string FindFixedRoot(string fullPattern)
        {
            var root = Path.GetPathRoot(fullPattern)!;
            var remainder = fullPattern.Substring(root.Length);
            var segments = remainder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            var fixedSegments = new List<string>();
            foreach (var segment in segments)
            {
                if (ContainsWildcard(segment))
                {
                    break;
                }

                fixedSegments.Add(segment);
            }

            if (fixedSegments.Count == 0)
            {
                return root;
            }

            return Path.Combine(new[] { root }.Concat(fixedSegments).ToArray());
        }

        private static bool ContainsWildcard(string value) => value.IndexOf('*') >= 0 || value.IndexOf('?') >= 0;

        private static string WildcardPatternToRegex(string wildcardPattern)
        {
            var escaped = Regex.Escape(wildcardPattern);
            escaped = escaped.Replace(@"\*\*", ".*");
            escaped = escaped.Replace(@"\*", @"[^\\]*");
            escaped = escaped.Replace(@"\?", @"[^\\]");
            return escaped;
        }

        /// <summary>
        /// Returns the path to the bundled esrpcli.dll that ships alongside this CLI tool.
        /// </summary>
        private static string GetBundledEsrpCliPath()
        {
            var appDir = AppContext.BaseDirectory;
            return Path.Combine(appDir, "ESRPCLI", "esrpcli.dll");
        }
    }
}

