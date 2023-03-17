// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.DeltaBuild;

internal record FilePath(string FullPath, bool Exists)
    : IEqualityComparer<FilePath>
{
    public static FilePath Create(string rawPath, string rootPath)
    {
        var fileInfo = !Path.IsPathRooted(rawPath)
               ? new FileInfo(Path.Join(rootPath, rawPath))
               : new FileInfo(rawPath);

        return new FilePath(fileInfo.FullName, fileInfo.Exists);
    }

    public static FilePath Create(string rawPath)
    {
        var fileInfo = new FileInfo(rawPath);
        return new(fileInfo.FullName, fileInfo.Exists);
    }

    public FilePath ChangeRoot(DirectoryInfo oldRoot, DirectoryInfo newRoot)
    {
        if (!FullPath.StartsWith(oldRoot.FullName))
        {
            throw new ArgumentException($"Full path {FullPath} doesn't start with {oldRoot}");
        }

        FileInfo fileInfo = new(FullPath.Replace(oldRoot.FullName, newRoot.FullName));
        return new(fileInfo.FullName, fileInfo.Exists);
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
