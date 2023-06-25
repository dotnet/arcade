// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Arcade.Common;

namespace Microsoft.DotNet.DeltaBuild;

public record FilePath(IFileSystem FileSystem, string FullPath)
    : IEqualityComparer<FilePath>
{
    public bool Exists => FileSystem.FileExists(FullPath);

    public static FilePath Create(string rawPath, string rootPath) =>
        Create(new FileSystem(), rawPath, rootPath);

    public static FilePath Create(IFileSystem fileSystem, string rawPath, string rootPath)
    {
        string fullPath = !Path.IsPathRooted(rawPath)
               ? Path.GetFullPath(Path.Combine(rootPath, rawPath))
               : Path.GetFullPath(rawPath);

        return new FilePath(fileSystem, fullPath);
    }

    public static FilePath Create(string rawPath) =>
        Create(new FileSystem(), rawPath);

    public static FilePath Create(IFileSystem filesystem, string rawPath)
    {
        string fullPath = Path.GetFullPath(rawPath);
        return new(filesystem, fullPath);
    }

    public FilePath ChangeRoot(DirectoryInfo oldRoot, DirectoryInfo newRoot) =>
        ChangeRoot(new FileSystem(), oldRoot, newRoot);

    public FilePath ChangeRoot(IFileSystem fileSystem, DirectoryInfo oldRoot, DirectoryInfo newRoot)
    {
        if (!FullPath.StartsWith(oldRoot.FullName))
        {
            throw new ArgumentException($"Full path {FullPath} doesn't start with {oldRoot}");
        }

        string newName = FullPath.Replace(oldRoot.FullName, newRoot.FullName);
        return new(fileSystem, newName);
    }

    public static implicit operator string(FilePath path)
    {
        return path.FullPath;
    }

    public bool Equals(FilePath? x, FilePath? y) =>
        x?.FullPath == y?.FullPath;

    public int GetHashCode(FilePath obj) =>
        obj.FullPath.GetHashCode();

    public override string ToString() => FullPath;
}
