// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Pkg;

if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    Console.Error.WriteLine("This tool is only supported on macOS.");
    return 1;
}

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: <src path> <dst path> <unpack|pack>");
    return 1;
}

string srcPath = args[0];
string dstPath = args[1];
string op = args[2];

try
{
    Processor.Initialize(srcPath, dstPath);

    if (op == "unpack")
    {
        Processor.Unpack();
    }
    else if(op == "pack")
    {
        Processor.Pack();
    }
    else
    {
        Console.Error.WriteLine($"Invalid operation {op}.");
        return 1;
    }
}
catch (Exception e)
{
    Console.Error.Write(e.Message);
    return 1;
}

return 0;
