// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class BatchHelixWorkItemsTests : IDisposable
    {
        private readonly string _root;

        public BatchHelixWorkItemsTests()
        {
            _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Fact]
        public void PayloadDirectoryWorkItemsAreBatched()
        {
            ITaskItem first = CreateWorkItem("First.Tests.dll", "dotnet exec First.Tests.dll", "00:01:00");
            ITaskItem second = CreateWorkItem("Second.Tests.dll", "dotnet exec Second.Tests.dll", "00:01:00");

            var task = CreateTask(first, second);

            task.Execute().Should().BeTrue();

            task.BatchedWorkItems.Should().ContainSingle();
            ITaskItem batch = task.BatchedWorkItems.Single();
            batch.ItemSpec.Should().StartWith("Batch_0001_First_Tests_dll");
            batch.GetMetadata("Command").Should().Be("run-batch.cmd");
            batch.GetMetadata("Timeout").Should().Be("00:04:00");
            batch.GetMetadata("BatchedWorkItemNames").Should().Be("First.Tests.dll;Second.Tests.dll");

            string batchPayload = batch.GetMetadata("PayloadDirectory");
            File.Exists(Path.Combine(batchPayload, "run-batch.cmd")).Should().BeTrue();
            File.Exists(Path.Combine(batchPayload, "batch-manifest.json")).Should().BeTrue();
            Directory.GetDirectories(Path.Combine(batchPayload, "payloads")).Should().HaveCount(2);
            File.ReadAllText(Path.Combine(batchPayload, "batch-manifest.json")).Should().Contain("First.Tests.dll");
            File.ReadAllText(Path.Combine(batchPayload, "run-batch.cmd")).Should().Contain("console.log");
        }

        [Fact]
        public void SingleEligibleWorkItemPassesThrough()
        {
            ITaskItem item = CreateWorkItem("Only.Tests.dll", "dotnet exec Only.Tests.dll", "00:01:00");

            var task = CreateTask(item);

            task.Execute().Should().BeTrue();

            task.BatchedWorkItems.Should().ContainSingle().Which.Should().BeSameAs(item);
        }

        [Fact]
        public void UnbatchableItemsArePreserved()
        {
            ITaskItem first = CreateWorkItem("First.Tests.dll", "dotnet exec First.Tests.dll", "00:01:00");
            ITaskItem skipped = CreateWorkItem("Skipped.Tests.dll", "dotnet exec Skipped.Tests.dll", "00:01:00");
            skipped.SetMetadata("HelixBatchable", "false");
            ITaskItem second = CreateWorkItem("Second.Tests.dll", "dotnet exec Second.Tests.dll", "00:01:00");

            var task = CreateTask(first, skipped, second);

            task.Execute().Should().BeTrue();

            task.BatchedWorkItems.Should().HaveCount(3);
            task.BatchedWorkItems[0].Should().BeSameAs(first);
            task.BatchedWorkItems[1].Should().BeSameAs(skipped);
            task.BatchedWorkItems[2].Should().BeSameAs(second);
        }

        [Fact]
        public void MaxItemsSplitsBatches()
        {
            var task = CreateTask(
                CreateWorkItem("First.Tests.dll", "dotnet exec First.Tests.dll", "00:01:00"),
                CreateWorkItem("Second.Tests.dll", "dotnet exec Second.Tests.dll", "00:01:00"),
                CreateWorkItem("Third.Tests.dll", "dotnet exec Third.Tests.dll", "00:01:00"),
                CreateWorkItem("Fourth.Tests.dll", "dotnet exec Fourth.Tests.dll", "00:01:00"));
            task.MaxItemsPerBatch = 2;

            task.Execute().Should().BeTrue();

            task.BatchedWorkItems.Should().HaveCount(2);
            task.BatchedWorkItems.All(i => i.ItemSpec.StartsWith("Batch_")).Should().BeTrue();
        }

        [Fact]
        public void PosixBatchUsesShellRunner()
        {
            var task = CreateTask(
                CreateWorkItem("First.Tests.dll", "dotnet exec First.Tests.dll", "00:01:00"),
                CreateWorkItem("Second.Tests.dll", "dotnet exec Second.Tests.dll", "00:01:00"));
            task.IsPosixShell = true;

            task.Execute().Should().BeTrue();

            ITaskItem batch = task.BatchedWorkItems.Single();
            batch.GetMetadata("Command").Should().Be("./run-batch.sh");
            File.ReadAllText(Path.Combine(batch.GetMetadata("PayloadDirectory"), "run-batch.sh")).Should().Contain("/bin/sh ./run-member.sh");
        }

        [Fact]
        public void ManifestContainsMemberMetadata()
        {
            var task = CreateTask(
                CreateWorkItem("First.Tests.dll", "dotnet exec First.Tests.dll", "00:01:00"),
                CreateWorkItem("Second.Tests.dll", "dotnet exec Second.Tests.dll", "00:02:00"));

            task.Execute().Should().BeTrue();

            string manifestPath = Path.Combine(task.BatchedWorkItems.Single().GetMetadata("PayloadDirectory"), "batch-manifest.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement manifest = document.RootElement;

            manifest.GetProperty("version").GetInt32().Should().Be(1);
            manifest.GetProperty("workItems").GetArrayLength().Should().Be(2);
            manifest.GetProperty("workItems")[1].GetProperty("timeout").GetString().Should().Be("00:02:00");
        }

        private BatchHelixWorkItems CreateTask(params ITaskItem[] workItems)
        {
            return new BatchHelixWorkItems
            {
                BuildEngine = new MockBuildEngine(),
                WorkItems = workItems,
                IntermediateOutputPath = Path.Combine(_root, "obj"),
                IsPosixShell = false,
                TargetDuration = 10,
                TimeoutPadding = 2,
                MaxItemsPerBatch = 10,
                MinItemsPerBatch = 2
            };
        }

        private ITaskItem CreateWorkItem(string name, string command, string timeout)
        {
            string payloadDirectory = Path.Combine(_root, "payloads", name);
            Directory.CreateDirectory(payloadDirectory);
            File.WriteAllText(Path.Combine(payloadDirectory, name), "payload");

            var result = new TaskItem(name);
            result.SetMetadata("PayloadDirectory", payloadDirectory);
            result.SetMetadata("Command", command);
            result.SetMetadata("Timeout", timeout);
            return result;
        }
    }
}
