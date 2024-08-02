// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.DiaSymReader.Tools;
using Microsoft.SymbolStore;

namespace Microsoft.DotNet.Internal.SymbolHelper;

public sealed class SymbolUploadHelper
{
    public const string ConversionFolderName = "_convertedPdbs";
    private const string AzureDevOpsResource = "499b84ac-1321-427f-aa17-267ca6975798";
    private const string PathEnvVarName = "AzureDevOpsToken";
    private static readonly FrozenSet<string> s_validExtensions = FrozenSet.ToFrozenSet(["", ".exe", ".dll", ".pdb", ".so", ".dbg", ".dylib", ".dwarf", ".r2rmap"]);
    private readonly ScopedTracerFactory _tracerFactory;
    private readonly ScopedTracer _globalTracer;
    private readonly string _workingDir;
    private readonly TokenCredential _credential;
    private readonly string _commonArgs;
    private readonly string _symbolToolPath;
    private readonly PdbConverter? _pdbConverter;
    private readonly bool _shouldGenerateManifest;
    private readonly bool _shouldConvertPdbs;
    private readonly bool _isDryRun;
    private readonly FrozenSet<string> _packageFileExclusions;
    private readonly bool _treatPdbConversionIssuesAsInfo;

    internal SymbolUploadHelper(ITracer logger, string symbolToolPath, SymbolPublisherOptions options, string? workingDir = null)
    {
        // These are all validated by the factory since this constructor is internal.
        // If these invariants change, the factory should be updated.
        Debug.Assert(logger is not null);
        Debug.Assert(options is not null);
        Debug.Assert(!string.IsNullOrEmpty(symbolToolPath) && (File.Exists(symbolToolPath) || options.IsDryRun));

        _tracerFactory = new ScopedTracerFactory(logger!);
        _globalTracer = _tracerFactory.CreateTracer(nameof(SymbolUploadHelper));

        _commonArgs = $"-s https://{options!.Tenant}.artifacts.visualstudio.com/ --patAuthEnvVar {PathEnvVarName}";
        if (options.VerboseClient)
        {
            // the true verbosity level is "verbose" but the tool is very chatty at that level.
            // "info" is a good balance for the errors that tend to come up in our layer.
            _commonArgs += " --tracelevel info";
        }
        else
        {
            _commonArgs += " --tracelevel warn";
        }

        _workingDir = workingDir ?? Path.GetTempPath();
        _credential = options.Credential;
        _symbolToolPath = symbolToolPath;
        _shouldGenerateManifest = options.DotnetInternalPublishSpecialClrFiles;
        _shouldConvertPdbs = options.ConvertPortablePdbs;
        _isDryRun = options.IsDryRun;
        _packageFileExclusions = options.PackageFileExcludeList;

        if (_shouldConvertPdbs)
        {
            _treatPdbConversionIssuesAsInfo = options.TreatPdbConversionIssuesAsInfo;
            _pdbConverter = new PdbConverter(diagnostic =>
            {
                string message = diagnostic.ToString();
                if (_treatPdbConversionIssuesAsInfo)
                {
                    _globalTracer.Information(message);
                }
                else if (options.PdbConversionTreatAsWarning.Contains((int)diagnostic.Id))
                {
                    _globalTracer.Warning(message);
                }
                else
                {
                    _globalTracer.Error(message);
                }
            });
        }
    }

    public async Task<int> GetClientDiagnosticInfo()
    {
        ScopedTracer logger = _tracerFactory.CreateTracer(nameof(GetClientDiagnosticInfo));
        logger.Information("Client Path: {0}", _symbolToolPath);
        return await RunSymbolCommand("help", ".", logger).ConfigureAwait(false);
    }

    public async Task<int> CreateRequest(string? name)
    {
        ScopedTracer logger = _tracerFactory.CreateTracer(nameof(CreateRequest));

        ValidateRequestName(name, logger);

        logger.Information("Creating symbol request: {0}", name!);
        string arguments = $"create {_commonArgs} --name {name}";
        return await RunSymbolCommand(arguments, ".", logger).ConfigureAwait(false);
    }

    public async Task<int> AddFiles(string? name, IEnumerable<string> files)
    {
        ScopedTracer logger = _tracerFactory.CreateTracer(nameof(AddFiles));
        ValidateRequestName(name, logger);

        // We create a folder and copy loose files to avoid starting a process per file.
        DirectoryInfo tempDirInfo = CreateTempDirectory();
        string tempCopyPath = tempDirInfo.FullName;
        try
        {
            foreach (string file in files)
            {
                using FileStream fs = File.OpenRead(file);
                string destPath = Path.Combine(tempCopyPath, Path.GetFileName(file));
                using FileStream fsCopy = new(destPath, FileMode.Create);
                await fs.CopyToAsync(fsCopy).ConfigureAwait(false);
            }

            if (_shouldConvertPdbs)
            {
                ConvertPortablePdbsInDirectory(logger, tempCopyPath);
            }

            logger.Information("Adding files to request {0}", name!);
            return await AddDirectoryCore(name!, tempCopyPath, manifestPath: null, logger).ConfigureAwait(false);
        }
        finally
        {
            logger.Information("Cleaning up temporary directory {0}", tempDirInfo.FullName);
            try { tempDirInfo.Delete(recursive: true); } catch { }
        }
    }

    public async Task<int> AddPackageToRequest(string? name, string packagePath)
    {
        ScopedTracer logger = _tracerFactory.CreateTracer(nameof(AddPackagesToRequest));
        ValidateRequestName(name, logger);
        string packageName = Path.GetFileName(packagePath);
        using IDisposable scopeToken = logger.AddSubScope(packageName);
        return await AddPackageToRequestCore(name!, packagePath, logger).ConfigureAwait(false);
    }

    public async Task<int> AddPackagesToRequest(string? name, IEnumerable<string> packagePaths)
    {
        ScopedTracer logger = _tracerFactory.CreateTracer(nameof(AddPackagesToRequest));
        ValidateRequestName(name, logger);

        int result = 0;

        foreach (string package in packagePaths)
        {
            string packageName = Path.GetFileName(package);
            using IDisposable scopeToken = logger.AddSubScope(packageName);
            result = await AddPackageToRequestCore(name!, package, logger).ConfigureAwait(false);
            if (result != 0)
            {
                break;
            }
        }
        return result;
    }

    public async Task<int> FinalizeRequest(string? name, uint daysToRetain)
    {
        ScopedTracer logger = _tracerFactory.CreateTracer(nameof(FinalizeRequest));
        ValidateRequestName(name, logger);

        logger.Information("Finalize symbol request: {0}", name!);
        string arguments = $"finalize {_commonArgs} --name {name} --expirationInDays {daysToRetain}";
        return await RunSymbolCommand(arguments, ".", logger).ConfigureAwait(false);
    }

    public async Task<int> DeleteRequest(string? name)
    {
        ScopedTracer logger = _tracerFactory.CreateTracer(nameof(DeleteRequest));
        ValidateRequestName(name, logger);
        logger.Information("Deleting symbol request: {0}", name!);
        string arguments = $"delete {_commonArgs} --name {name} --quiet --synchronous";
        return await RunSymbolCommand(arguments, ".", logger).ConfigureAwait(false);
    }

    private async Task<int> AddDirectoryCore(string name, string pathToAdd, string? manifestPath, ScopedTracer logger)
    {
        logger.Information("Adding directory {0} to request {1}", pathToAdd, name);
        string arguments = $"adddirectory {_commonArgs} -n {name} --directory {pathToAdd} --recurse true";

        if (manifestPath is not null)
        {
            arguments += " --manifest " + manifestPath;
        }

        return await RunSymbolCommand(arguments, pathToAdd, logger).ConfigureAwait(false);
    }

    private async Task<int> AddPackageToRequestCore(string name, string packagePath, ScopedTracer logger)
    {
        // Create a temporary directory to extract the package contents.
        DirectoryInfo packageDirInfo = CreateTempDirectory();
        string packageExtractDir = packageDirInfo.FullName;
        try
        {
            using ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Read);

            logger.Information("Extracting symbol package {0} to {1}", packagePath, packageExtractDir);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/'))
                {
                    Debug.Assert(entry.Length == 0);
                    continue;
                }

                if (!ShouldIndexPackageFile(entry.FullName))
                {
                    logger.Verbose("Skipping {0}", entry.FullName);
                    continue;
                }

                logger.Verbose("Extracting {0}", entry.FullName);
                string entryPath = Path.Combine(packageExtractDir, entry.FullName);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
                using Stream entryStream = entry.Open();
                using FileStream entryFile = File.Create(entryPath);
                await entryStream.CopyToAsync(entryFile).ConfigureAwait(false);
            }

            if (_shouldConvertPdbs)
            {
                ConvertPortablePdbsInDirectory(logger, packageExtractDir);
            }

            string? manifest = null;
            if (_shouldGenerateManifest)
            {
                manifest = Path.Combine(packageExtractDir, "correlatedSymKeysManifest.json");
                if (!SymbolManifestGenerator.SymbolManifestGenerator.GenerateManifest(logger, packageDirInfo, manifest, specialFilesRequireAdjacentRuntime: false))
                {
                    logger.Error("Failed to generate symbol manifest");
                    return -1;
                }
                logger.Verbose("Generated manifest in {0}", manifest);
            }

            logger.Information("Adding package {0} to request {1}", packagePath, name);
            return await AddDirectoryCore(name, packageExtractDir, manifest, logger).ConfigureAwait(false);
        }
        finally
        {
            logger.Information("Cleaning up temporary directory {0}", packageDirInfo.FullName);
            try { packageDirInfo.Delete(recursive: true); } catch {}
        }

        bool ShouldIndexPackageFile(string relativeFilePath)
        {
            if (relativeFilePath.StartsWith("ref/")
                || relativeFilePath.StartsWith("_rels/")
                || relativeFilePath.StartsWith("package/")
                || relativeFilePath.EndsWith("_.pdb"))
            {
                // Quick bail - special nupkg files and ref assemblies are not indexed.
                return false;
            }

            relativeFilePath = relativeFilePath.Replace("//", "/");

            if (_packageFileExclusions.Contains(relativeFilePath))
            {
                return false;
            }

            string extension = Path.GetExtension(relativeFilePath);
            return s_validExtensions.Contains(extension);
        }
    }

    private void ConvertPortablePdbsInDirectory(ScopedTracer logger, string filesDir)
    {
        Action<string> logWarning = _treatPdbConversionIssuesAsInfo ? logger.Information : logger.Error;
        string convertedPdbFolder = Path.Combine(filesDir, ConversionFolderName);
        _ = Directory.CreateDirectory(convertedPdbFolder);
        foreach (string file in Directory.EnumerateFiles(filesDir, "*.pdb", SearchOption.AllDirectories))
        {
            using Stream pdbStream = File.OpenRead(file);
            if (!PdbConverter.IsPortable(pdbStream))
            {
                continue;
            }

            logger.Verbose("Converting {0} to portable format", file);

            string pePath = Path.ChangeExtension(file, ".dll");
            // Try to fall back to the framework exe scenario.
            if (!File.Exists(pePath))
            {
                pePath = Path.ChangeExtension(file, ".exe");
            }

            if (!File.Exists(pePath))
            {
                logWarning($"Conversion error: could not find matching PE file for {file}");
                continue;
            }

            string convertedPdbPath = Path.Combine(convertedPdbFolder, Path.GetFileName(file));

            try
            {
                using Stream peStream = File.OpenRead(pePath);
                using Stream convertedPdbStream = File.Create(convertedPdbPath);
                _pdbConverter!.ConvertWindowsToPortable(peStream, pdbStream, convertedPdbStream);
            }
            catch (Exception ex)
            {
                logWarning($"Conversion error: {ex.Message}");
                continue;
            }

            logger.Verbose("Converted successfully to {0}.", convertedPdbPath);
        }
    }

    private DirectoryInfo CreateTempDirectory()
    {
        string tempDir = Path.Combine(_workingDir, Path.GetRandomFileName());
        while (Directory.Exists(tempDir) || File.Exists(tempDir))
        {
            tempDir = Path.Combine(_workingDir, Path.GetRandomFileName());
        }

        return Directory.CreateDirectory(tempDir);
    }

    private static void ValidateRequestName(string? name, ScopedTracer logger)
    {
        if (name is null or "")
        {
            logger.Error("Can't create a request with an empty name.");
            throw new ArgumentException("Name must be specified", nameof(name));
        }
    }

    private async Task<int> RunSymbolCommand(string arguments, string directory, ITracer logger, CancellationToken ct = default)
    {
        // TODO: Add retry logic. Need to parse output stream for this.
        logger.Verbose("Running command: {0} {1} from '{2}'", _symbolToolPath, arguments, directory);
        if (_isDryRun)
        {
            logger.Information("Would run command: {0} {1} from '{2}'", _symbolToolPath, arguments, directory);
            return 0;
        }

        AccessToken token = await _credential.GetTokenAsync(new TokenRequestContext([AzureDevOpsResource]), ct).ConfigureAwait(false);
        ProcessStartInfo info = new(_symbolToolPath, arguments)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = directory,
            Environment = { [PathEnvVarName] = token.Token }
        };

        using Process process = new()
        {
            StartInfo = info
        };

        _ = process.Start();
        Task processExit = process.WaitForExitAsync(ct);

        // This sentinel task is used to indicate that the output has been fully read. It's never completed.
        TaskCompletionSource<string?> outputFinishedSentinel = new();

        StreamReader standardOutput = process.StandardOutput;
        Task<string?> outputAvailable = standardOutput.ReadLineAsync(ct).AsTask();

        StreamReader standardError = process.StandardError;
        Task<string?> errorAvailable = standardError.ReadLineAsync(ct).AsTask();

        // TODO: handle time capping & cancelation.
        while (!processExit.IsCompleted && !ct.IsCancellationRequested)
        {
            Task alertedTask = await Task.WhenAny(outputAvailable, errorAvailable, processExit).ConfigureAwait(false);

            if (alertedTask == outputAvailable)
            {
                outputAvailable = await LogFromStreamReader(outputAvailable, standardOutput.ReadLineAsync, logger.Information);
            }
            else if (alertedTask == errorAvailable)
            {
                errorAvailable = await LogFromStreamReader(errorAvailable, standardError.ReadLineAsync, logger.Error);
            }
        }

        if (ct.IsCancellationRequested && processExit.IsCompleted)
        {
            process.Kill();
        }

        while (!standardOutput.EndOfStream)
        {
            string? line = await standardOutput.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is not null) { logger.Information(line); }
        }

        while (!standardError.EndOfStream)
        {
            string? line = await standardError.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is not null) { logger.Error(line); }
        }

        return process.ExitCode;

        async Task<Task<string?>> LogFromStreamReader(Task<string?> outputTask, Func<Task<string?>> readLine, Action<string> logMethod)
        {
            string? line = await outputTask.ConfigureAwait(false);

            if (line is not null)
            {
                logMethod(line);
                outputTask = readLine();
            }
            else
            {
                outputTask = outputFinishedSentinel.Task;
            }

            return outputTask;
        }
    }
}
