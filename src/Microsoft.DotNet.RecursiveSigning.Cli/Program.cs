// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
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
            if (!TryParseArguments(args, out var options))
            {
                PrintUsage();
                return 1;
            }

            if (!File.Exists(options.ConfigPath))
            {
                Console.Error.WriteLine($"Error: Config file not found: {options.ConfigPath}");
                return 1;
            }

            DefaultCertificateRules rules;
            try
            {
                var reader = new DefaultCertificateRulesReader();
                rules = reader.ReadFromFile(options.ConfigPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Failed to read config file '{options.ConfigPath}': {ex.Message}");
                return 1;
            }

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information));
            services.AddRecursiveSigning();
            services.AddContainerHandler<ZipContainerHandler>();
            services.AddSingleton<IFileAnalyzer, DefaultFileAnalyzer>();
            services.AddSingleton<ICertificateCalculator>(_ => new DefaultCertificateCalculator(rules));

            if (options.UseESRP)
            {
                var usePME = options.UseFederatedToken;

                // Validate required ESRP config when not in dry-run
                if (!options.DryRun)
                {
                    var missing = new List<string>();
                    if (string.IsNullOrEmpty(options.EsrpId)) missing.Add("--esrp-id");
                    if (string.IsNullOrEmpty(options.ESRPClientId)) missing.Add("--esrp-client-id");
                    if (string.IsNullOrEmpty(options.ESRPTenantId)) missing.Add("--esrp-tenant-id");
                    if (string.IsNullOrEmpty(options.ESRPKeyVaultName)) missing.Add("--esrp-keyvault-name");
                    if (string.IsNullOrEmpty(options.ESRPCertName)) missing.Add("--esrp-cert-name");
                    if (usePME && string.IsNullOrEmpty(options.ServiceConnectionId)) missing.Add("--service-connection-id");
                    if (missing.Count > 0)
                    {
                        Console.Error.WriteLine($"Error: The following required ESRP options are missing: {string.Join(", ", missing)}");
                        return 1;
                    }
                }

                var esrpConfig = new ESRPCliSigningConfiguration
                {
                    ESRPCliPath = options.ESRPCliPath ?? GetBundledEsrpCliPath(),
                    TempDirectory = options.TempDirectory,
                    RootDirectory = options.RootDirectory ?? Directory.GetCurrentDirectory(),
                    DryRun = options.DryRun,
                    VerboseLogging = options.Verbose,
                    AuthMode = usePME ? ESRPAuthMode.FederatedToken : ESRPAuthMode.Certificate,
                    EsrpClientId = options.EsrpId ?? "",
                    ClientId = options.ESRPClientId ?? "",
                    TenantId = options.ESRPTenantId ?? "",
                    KeyVaultName = options.ESRPKeyVaultName ?? "",
                    CertificateName = options.ESRPCertName ?? "",
                    ServiceConnectionId = options.ServiceConnectionId ?? "",
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

            var resolvedInputs = ExpandInputPaths(options.InputPatterns);
            if (resolvedInputs.Count == 0)
            {
                Console.Error.WriteLine("No input files were resolved from the provided --input arguments.");
                return 1;
            }

            var request = new SigningRequest(
                resolvedInputs.Select(path => new FileInfo(path)).ToArray(),
                new SigningConfiguration(options.TempDirectory, options.OutputDirectory),
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

        private static bool TryParseArguments(string[] args, out CliOptions options)
        {
            options = new CliOptions("", "", null, Array.Empty<string>(), Verbose: false,
                UseESRP: false, DryRun: false, UseFederatedToken: false, ESRPCliPath: null, RootDirectory: null,
                EsrpId: null, ESRPClientId: null, ESRPTenantId: null, ESRPKeyVaultName: null, ESRPCertName: null,
                ServiceConnectionId: null);

            if (args.Length < 3)
            {
                return false;
            }

            string? configPath = null;
            string tempDirectory = Path.Combine(Path.GetTempPath(), "recursive-signing-cli");
            string? outputDirectory = null;
            var inputPatterns = new List<string>();
            var verbose = false;
            var useESRP = false;
            var dryRun = false;
            var useFederatedToken = false;
            string? esrpCliPath = null;
            string? rootDirectory = null;
            string? esrpId = null;
            string? esrpClientId = null;
            string? esrpTenantId = null;
            string? esrpKeyVaultName = null;
            string? esrpCertName = null;
            string? serviceConnectionId = null;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    configPath = args[++i];
                }
                else if (arg.Equals("--temp", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    tempDirectory = args[++i];
                }
                else if (arg.Equals("--input", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    inputPatterns.Add(args[++i]);
                }
                else if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    outputDirectory = args[++i];
                }
                else if (arg.Equals("--esrp", StringComparison.OrdinalIgnoreCase))
                {
                    useESRP = true;
                }
                else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
                {
                    dryRun = true;
                }
                else if (arg.Equals("--esrp-cli-path", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    esrpCliPath = args[++i];
                }
                else if (arg.Equals("--root", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    rootDirectory = args[++i];
                }
                else if (arg.Equals("--esrp-id", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    esrpId = args[++i];
                }
                else if (arg.Equals("--esrp-client-id", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    esrpClientId = args[++i];
                }
                else if (arg.Equals("--esrp-tenant-id", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    esrpTenantId = args[++i];
                }
                else if (arg.Equals("--esrp-keyvault-name", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    esrpKeyVaultName = args[++i];
                }
                else if (arg.Equals("--esrp-cert-name", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    esrpCertName = args[++i];
                }
                else if (arg.Equals("--federated-token", StringComparison.OrdinalIgnoreCase))
                {
                    useFederatedToken = true;
                }
                else if (arg.Equals("--service-connection-id", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    serviceConnectionId = args[++i];
                }
                else if (arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase))
                {
                    verbose = true;
                }
                else
                {
                    inputPatterns.Add(arg);
                }
            }

            if (string.IsNullOrWhiteSpace(configPath) || inputPatterns.Count == 0)
            {
                return false;
            }

            options = new CliOptions(configPath, tempDirectory, outputDirectory, inputPatterns, verbose,
                useESRP, dryRun, useFederatedToken, esrpCliPath, rootDirectory,
                esrpId, esrpClientId, esrpTenantId, esrpKeyVaultName, esrpCertName, serviceConnectionId);
            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  recursive-signing-cli --config <rules.json> --input <file-or-glob> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --input <file-or-glob>    Top-level files to process (repeatable)");
            Console.WriteLine("  --temp <directory>        Temporary working directory");
            Console.WriteLine("  --output <directory>      Output directory for final root artifacts");
            Console.WriteLine("  --verbose                 Enable debug logging");
            Console.WriteLine("  --esrp                    Use ESRP CLI signing provider instead of dry-run");
            Console.WriteLine("  --dry-run                 Print ESRP submission JSON without invoking CLI (requires --esrp)");
            Console.WriteLine("  --esrp-cli-path <path>    Path to esrpcli.dll (default: bundled copy; requires --esrp)");
            Console.WriteLine("  --root <directory>        Root directory for relative file paths (requires --esrp)");
            Console.WriteLine("  --esrp-id <id>            ESRP client account identifier (-esrpClientId flag)");
            Console.WriteLine("  --esrp-client-id <id>     AAD app registration client ID for auth (-a flag)");
            Console.WriteLine("  --esrp-tenant-id <id>     AAD tenant ID (-d flag)");
            Console.WriteLine("  --esrp-keyvault-name <n>  Key vault name for ESRP auth cert");
            Console.WriteLine("  --esrp-cert-name <name>   Certificate name in key vault");
            Console.WriteLine("  --federated-token         Use federated token (PME) auth mode (default: certificate/CORP)");
            Console.WriteLine("  --service-connection-id <guid>  ADO service connection GUID for federated token auth");
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

        private sealed record CliOptions(
            string ConfigPath,
            string TempDirectory,
            string? OutputDirectory,
            IReadOnlyList<string> InputPatterns,
            bool Verbose,
            bool UseESRP,
            bool DryRun,
            bool UseFederatedToken,
            string? ESRPCliPath,
            string? RootDirectory,
            string? EsrpId,
            string? ESRPClientId,
            string? ESRPTenantId,
            string? ESRPKeyVaultName,
            string? ESRPCertName,
            string? ServiceConnectionId);
    }
}

