// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.VersionTools.Automation;

namespace Microsoft.DotNet.VersionTools.Cli;


/// <summary>
/// Utilize Microsoft.DotNet.VersionTool to remove the version from the nuget asset file name.
/// </summary>
public class VersionTrimmingOperation : IOperation
{
    public struct Context
    {
        public INupkgInfoFactory NupkgInfoFactory { get; set; }
        public IFileProxy FileProxy { get; set; }
        public IDirectoryProxy DirectoryProxy { get; set; }

        public string AssetsDirectory { get; set; }
        public string SearchPattern { get; set; }
        public bool Recursive { get; set; }
    }

    private readonly Context _context;

    public VersionTrimmingOperation(Context context) => _context = context;

    public IOperation.ExitCodes Execute()
    {
        if (!_context.DirectoryProxy.Exists(_context.AssetsDirectory))
        {
            Console.WriteLine($"{_context.AssetsDirectory} does not exist");
            return IOperation.ExitCodes.ERROR_FILE_NOT_FOUND;
        }

        foreach (var assetFileName in _context.DirectoryProxy.GetFiles(
            _context.AssetsDirectory,
            _context.SearchPattern,
            _context.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        {
            NupkgInfo info = null;
            try
            {
                info = _context.NupkgInfoFactory.CreateNupkgInfo(assetFileName);
            }
            catch (InvalidDataException e)
            {
                Console.WriteLine($"Asset {assetFileName} in not a valid nuget package: {e.Message}");
                continue;
            }

            string newAssetFileName = assetFileName.Replace($".{info.Version}", null);
            if (assetFileName == newAssetFileName) continue;

            Console.WriteLine($"Processing {assetFileName} ...");

            try
            {
                _context.FileProxy.Move(assetFileName, newAssetFileName);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Version trimming of {assetFileName} failed: {e.Message}");
                continue;
            }
        }

        return IOperation.ExitCodes.ERROR_SUCCESS;
    }
}
