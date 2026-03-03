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

            var reader = new DefaultCertificateRulesReader();
            var rules = reader.ReadFromFile(options.ConfigPath);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information));
            services.AddRecursiveSigning();
            services.AddContainerHandler<ZipContainerHandler>();
            services.AddSingleton<IFileAnalyzer, DefaultFileAnalyzer>();
            services.AddSingleton<ISigningProvider, DryRunSigningProvider>();
            services.AddSingleton<ICertificateCalculator>(_ => new DefaultCertificateCalculator(rules));

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

            return result.Success ? 0 : 1;
        }

        private static bool TryParseArguments(string[] args, out CliOptions options)
        {
            options = new CliOptions("", "", null, Array.Empty<string>(), Verbose: false);

            if (args.Length < 3)
            {
                return false;
            }

            string? configPath = null;
            string tempDirectory = Path.Combine(Path.GetTempPath(), "recursive-signing-cli");
            string? outputDirectory = null;
            var inputPatterns = new List<string>();
            var verbose = false;

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

            options = new CliOptions(configPath, tempDirectory, outputDirectory, inputPatterns, verbose);
            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  recursive-signing-cli --config <rules.json> --input <file-or-glob> [--input <file-or-glob> ...] [--temp <tempDirectory>] [--output <outputDirectory>] [--verbose]");
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

        private sealed record CliOptions(string ConfigPath, string TempDirectory, string? OutputDirectory, IReadOnlyList<string> InputPatterns, bool Verbose);
    }
}


