// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class GenerateRuntimeGraphTests
    {
        private Log _log;
        private TestBuildEngine _engine;

        public GenerateRuntimeGraphTests(ITestOutputHelper output)
        {
            _log = new Log(output);
            _engine = new TestBuildEngine(_log);
        }

        [Fact]
        public void CanCreateRuntimeGraph()
        {
            string runtimeFile = $"{nameof(GenerateRuntimeGraphTests)}.{nameof(CanCreateRuntimeGraph)}.runtime.json";

            // will generate and compare to existing file.
            GenerateRuntimeGraph task = new GenerateRuntimeGraph()
            {
                BuildEngine = _engine,
                RuntimeGroups = runtimeGroups,
                RuntimeJson = runtimeFile
            };

            _log.Reset();
            task.Execute();
            _log.ErrorsLogged.Should().Be(0);
            _log.WarningsLogged.Should().Be(0);
        }

        [Fact]
        public void CanInferRids()
        {
            string runtimeFile = $"{nameof(GenerateRuntimeGraphTests)}.{nameof(CanInferRids)}.runtime.json";

            // will generate and compare to existing file.
            GenerateRuntimeGraph task = new GenerateRuntimeGraph()
            {
                BuildEngine = _engine,
                RuntimeGroups = runtimeGroups,
                RuntimeJson = runtimeFile,
                InferRuntimeIdentifiers = new[] { "rhel.9.2-x64", "centos.9.2-arm64" }
            };

            _log.Reset();
            task.Execute();
            _log.ErrorsLogged.Should().Be(0);
            _log.WarningsLogged.Should().Be(0);
        }

        [Fact]
        public void CanIgnoreExistingInferRids()
        {
            // intentionally use same file as CanCreateRuntimeGraph, we don't want changes
            string runtimeFile = $"{nameof(GenerateRuntimeGraphTests)}.{nameof(CanCreateRuntimeGraph)}.runtime.json";

            // will generate and compare to existing file.
            GenerateRuntimeGraph task = new GenerateRuntimeGraph()
            {
                BuildEngine = _engine,
                RuntimeGroups = runtimeGroups,
                RuntimeJson = runtimeFile,
                InferRuntimeIdentifiers = new[] { "rhel.9-x64", "centos.9-arm64" }
            };

            _log.Reset();
            task.Execute();
            _log.ErrorsLogged.Should().Be(0);
            _log.WarningsLogged.Should().Be(0);
        }

        // a subset of the actual runtimegraph that excercises most of the functionality of the task.
        static ITaskItem[] runtimeGroups = new[]
        {
            CreateRuntimeGroup("any"),
            CreateRuntimeGroup("aot"),
            CreateRuntimeGroup("unix", "any", "x64;x86;arm;armel;arm64;mips64"),
            CreateRuntimeGroup("linux", "unix", "x64;x86;arm;armel;arm64;mips64"),
            CreateRuntimeGroup("osx", "unix", "x64;arm64", "10.10;10.11;10.12;10.13;10.14;10.15;10.16;11.0"),
            CreateRuntimeGroup("win", "any", "x64;x86;arm;arm64", "7;8;81;10", additionalQualifiers:"aot", omitVersionDelimiter:true),
            CreateRuntimeGroup("debian", "linux", "x64;x86;arm;armel;arm64", "8;9;10", treatVersionsAsCompatible:false),
            CreateRuntimeGroup("rhel", "linux", "x64", "6"),
            CreateRuntimeGroup("rhel", "linux", "x64", "7;7.0;7.1;7.2;7.3;7.4;7.5;7.6"),
            CreateRuntimeGroup("rhel", "linux", "x64;arm64", "8;8.0;8.1"),
            CreateRuntimeGroup("rhel", "linux", "x64;arm64", "9"),
            CreateRuntimeGroup("centos", "rhel", "x64", "7", applyVersionsToParent:true, treatVersionsAsCompatible:false),
            CreateRuntimeGroup("centos", "rhel", "x64;arm64", "8;9", applyVersionsToParent:true, treatVersionsAsCompatible:false)
        };

        private static ITaskItem CreateRuntimeGroup(string name, string parent = null, string architectures = null, string versions = null, string additionalQualifiers = null, bool applyVersionsToParent = false, bool treatVersionsAsCompatible = true, bool omitVersionDelimiter = false)
        {
            TaskItem item = new TaskItem(name);

            if (parent != null)
            {
                item.SetMetadata("Parent", parent);
            }

            if (architectures != null)
            {
                item.SetMetadata("Architectures", architectures);
            }

            if (versions != null)
            {
                item.SetMetadata("Versions", versions);
            }

            if (applyVersionsToParent)
            {
                item.SetMetadata("ApplyVersionsToParent", applyVersionsToParent.ToString());
            }

            if (!treatVersionsAsCompatible)
            {
                item.SetMetadata("TreatVersionsAsCompatible", treatVersionsAsCompatible.ToString());
            }

            if (omitVersionDelimiter)
            {
                item.SetMetadata("OmitVersionDelimiter", omitVersionDelimiter.ToString());
            }

            if (additionalQualifiers != null)
            {
                item.SetMetadata("AdditionalQualifiers", additionalQualifiers);
            }

            return item;
        }
    }
}
