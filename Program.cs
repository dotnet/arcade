// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SignTool
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            SignToolArgs signToolArgs;
            if (!ParseCommandLineArguments(args, out signToolArgs))
            {
                PrintUsage();
                Environment.Exit(1);
            }

            var signTool = SignToolFactory.Create(signToolArgs);
            var batchData = ReadBatchSignInput(signToolArgs.OutputPath);
            var util = new BatchSignUtil(signTool, batchData);
            util.Go();
        }

        internal static BatchSignInput ReadBatchSignInput(string rootBinaryPath)
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "BatchSignData.json");
            using (var file = File.OpenText(filePath))
            {
                var serializer = new JsonSerializer();
                var fileJson = (Json.FileJson)serializer.Deserialize(file, typeof(Json.FileJson));
                var map = new Dictionary<string, SignInfo>();
                var allGood = true;
                foreach (var item in fileJson.SignList)
                {
                    var data = new SignInfo(certificate: item.Certificate, strongName: item.StrongName);
                    foreach (var name in item.FileList)
                    {
                        if (map.ContainsKey(name))
                        {
                            Console.WriteLine($"Duplicate file entry: {name}");
                            allGood = false;
                        }
                        else
                        {
                            map.Add(name, data);
                        }
                    }
                }

                if (!allGood)
                {
                    Environment.Exit(1);
                }

                return new BatchSignInput(rootBinaryPath, map, fileJson.ExcludeList);
            }
        }

        internal static void PrintUsage()
        {
            var usage =
@"SignTool.exe [-test] [-intermediateOutputPath <path>] [-msbuildPath <path>] outputPath

test: Run tool without actually modifying any state.
outputPath: Directory containing the binaries.
intermediateOutputPath: Directory containing intermediate output.  Default is (outputpath\..\Obj)
msbuildPath: Path to MSBuild.exe to use as signing mechanism.
";
            Console.WriteLine(usage);
        }

        internal static bool ParseCommandLineArguments(
            string[] args,
            out SignToolArgs signToolArgs)
        {
            signToolArgs = default(SignToolArgs);

            string intermediateOutputPath = null;
            string outputPath = null;
            string msbuildPath = null;
            var test = false;

            var i = 0;
            while (i + 1 < args.Length)
            {
                var current = args[i];
                switch (current.ToLower())
                {
                    case "-test":
                        test = true;
                        i++;
                        break;
                    case "-intermediateOutputPath":
                        if (i + 1 >= args.Length)
                        {
                            Console.WriteLine("-binariesPath needs an argument");
                            return false;
                        }

                        intermediateOutputPath = args[i + 1];
                        i += 2;
                        break;
                    case "-msbuildpath":
                        if (i + 1 >= args.Length)
                        {
                            Console.WriteLine("-msbuildPath needs an argument");
                            return false;
                        }

                        msbuildPath = args[i + 1];
                        i += 2;
                        break;
                    default:
                        Console.WriteLine($"Unrecognized option {current}");
                        return false;
                }
            }

            if (i + 1 != args.Length)
            {
                Console.WriteLine("Need a value for outputPath");
                return false;
            }

            outputPath = args[i];

            // Get defaults for all of the optional values that weren't specified
            msbuildPath = msbuildPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"MSBuild\14.0\bin\MSBuild.exe");
            intermediateOutputPath = intermediateOutputPath ?? Path.Combine(Path.GetDirectoryName(outputPath), "Obj");

            signToolArgs = new SignToolArgs(
                outputPath: outputPath,
                msbuildPath: msbuildPath,
                intermediateOutputPath: intermediateOutputPath,
                appPath: AppContext.BaseDirectory,
                test: test);
            return true;
        }
    }
}
