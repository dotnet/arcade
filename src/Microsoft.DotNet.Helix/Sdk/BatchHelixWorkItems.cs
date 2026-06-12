// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class BatchHelixWorkItems : BaseTask
    {
        private const int DefaultTargetDurationMinutes = 10;
        private const int DefaultTimeoutPaddingMinutes = 2;

        public ITaskItem[] WorkItems { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string IntermediateOutputPath { get; set; }

        [Required]
        public bool IsPosixShell { get; set; }

        public int TargetDuration { get; set; } = DefaultTargetDurationMinutes;

        public int TimeoutPadding { get; set; } = DefaultTimeoutPaddingMinutes;

        public int MaxItemsPerBatch { get; set; } = 10;

        public int MinItemsPerBatch { get; set; } = 2;

        [Output]
        public ITaskItem[] BatchedWorkItems { get; set; }

        public override bool Execute()
        {
            TimeSpan targetDuration = TimeSpan.FromMinutes(Math.Max(1, TargetDuration));
            TimeSpan timeoutPadding = TimeSpan.FromMinutes(Math.Max(0, TimeoutPadding));
            int maxItemsPerBatch = Math.Max(1, MaxItemsPerBatch);
            int minItemsPerBatch = Math.Max(1, MinItemsPerBatch);

            string batchRoot = Path.GetFullPath(Path.Combine(IntermediateOutputPath, "helix-work-item-batches"));
            if (Directory.Exists(batchRoot))
            {
                Directory.Delete(batchRoot, recursive: true);
            }
            Directory.CreateDirectory(batchRoot);

            var results = new List<ITaskItem>();
            var currentBatch = new List<BatchMember>();
            TimeSpan currentDuration = TimeSpan.Zero;
            int batchNumber = 1;

            foreach (ITaskItem workItem in WorkItems ?? Array.Empty<ITaskItem>())
            {
                if (!TryCreateBatchMember(workItem, out BatchMember member))
                {
                    FlushBatchIfNeeded();
                    results.Add(workItem);
                    continue;
                }

                bool wouldExceedTarget = currentBatch.Count > 0 && currentDuration + member.ExpectedDuration > targetDuration;
                bool wouldExceedCount = currentBatch.Count >= maxItemsPerBatch;
                if (wouldExceedTarget || wouldExceedCount)
                {
                    FlushBatchIfNeeded();
                }

                currentBatch.Add(member);
                currentDuration += member.ExpectedDuration;
            }

            FlushBatchIfNeeded();
            BatchedWorkItems = results.ToArray();
            return !Log.HasLoggedErrors;

            void FlushBatchIfNeeded()
            {
                if (currentBatch.Count == 0)
                {
                    return;
                }

                if (currentBatch.Count < minItemsPerBatch)
                {
                    results.AddRange(currentBatch.Select(m => m.WorkItem));
                }
                else
                {
                    results.Add(CreateBatchWorkItem(currentBatch, batchRoot, batchNumber++, timeoutPadding));
                }

                currentBatch = new List<BatchMember>();
                currentDuration = TimeSpan.Zero;
            }
        }

        private bool TryCreateBatchMember(ITaskItem workItem, out BatchMember member)
        {
            member = null;

            string name = GetMetadataOrItemSpec(workItem, SendHelixJob.MetadataNames.Identity);
            string command = workItem.GetMetadata(SendHelixJob.MetadataNames.Command);
            string payloadDirectory = workItem.GetMetadata(SendHelixJob.MetadataNames.PayloadDirectory);
            string payloadArchive = workItem.GetMetadata(SendHelixJob.MetadataNames.PayloadArchive);
            string payloadUri = workItem.GetMetadata(SendHelixJob.MetadataNames.PayloadUri);
            string preCommands = workItem.GetMetadata(SendHelixJob.MetadataNames.PreCommands);
            string postCommands = workItem.GetMetadata(SendHelixJob.MetadataNames.PostCommands);
            string batchable = workItem.GetMetadata("HelixBatchable");

            if (string.Equals(batchable, "false", StringComparison.OrdinalIgnoreCase))
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping Helix work item '{name}' because HelixBatchable=false.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(payloadDirectory))
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping Helix work item '{name}' because it does not have a simple PayloadDirectory and Command.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(payloadArchive) || !string.IsNullOrWhiteSpace(payloadUri))
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping Helix work item '{name}' because archive and URI payload batching is not supported.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(preCommands) || !string.IsNullOrWhiteSpace(postCommands))
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping Helix work item '{name}' because per-work-item pre/post commands are not supported by batching.");
                return false;
            }

            if (!Directory.Exists(payloadDirectory))
            {
                Log.LogWarning($"Skipping Helix work item '{name}' because payload directory '{payloadDirectory}' does not exist.");
                return false;
            }

            TimeSpan expectedDuration = GetExpectedDuration(workItem);
            TimeSpan timeout = GetTimeout(workItem, expectedDuration);
            member = new BatchMember(workItem, name, command, payloadDirectory, expectedDuration, timeout);
            return true;
        }

        private ITaskItem CreateBatchWorkItem(IReadOnlyList<BatchMember> members, string batchRoot, int batchNumber, TimeSpan timeoutPadding)
        {
            string batchName = $"Batch_{batchNumber:0000}_{SanitizeName(members[0].Name)}";
            string batchDirectory = Path.Combine(batchRoot, batchName);
            string payloadsDirectory = Path.Combine(batchDirectory, "payloads");
            Directory.CreateDirectory(payloadsDirectory);

            var manifestEntries = new List<ManifestEntry>();
            for (int i = 0; i < members.Count; i++)
            {
                BatchMember member = members[i];
                string memberDirectoryName = $"{i + 1:000}_{ShortHash(member.Name)}";
                string memberPayloadDirectory = Path.Combine(payloadsDirectory, memberDirectoryName);
                CopyDirectory(member.PayloadDirectory, memberPayloadDirectory);
                WriteMemberCommand(batchDirectory, memberDirectoryName, member.Command);
                manifestEntries.Add(new ManifestEntry
                {
                    Name = member.Name,
                    Command = member.Command,
                    PayloadDirectory = $"payloads/{memberDirectoryName}",
                    Timeout = member.Timeout.ToString(),
                    ExpectedDuration = member.ExpectedDuration.ToString()
                });
            }

            File.WriteAllText(
                Path.Combine(batchDirectory, "batch-manifest.json"),
                JsonConvert.SerializeObject(new BatchManifest { Version = 1, WorkItems = manifestEntries }, Formatting.Indented),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string runnerFileName = IsPosixShell ? "run-batch.sh" : "run-batch.cmd";
            File.WriteAllText(
                Path.Combine(batchDirectory, runnerFileName),
                IsPosixShell ? CreatePosixRunner(manifestEntries) : CreateWindowsRunner(manifestEntries),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            TimeSpan timeout = TimeSpan.FromTicks(members.Sum(m => m.Timeout.Ticks) + timeoutPadding.Ticks);
            var batchWorkItem = new TaskItem(batchName, new Dictionary<string, string>
            {
                [SendHelixJob.MetadataNames.PayloadDirectory] = batchDirectory,
                [SendHelixJob.MetadataNames.Command] = IsPosixShell ? "./run-batch.sh" : "run-batch.cmd",
                [SendHelixJob.MetadataNames.Timeout] = timeout.ToString(),
                ["BatchedWorkItemNames"] = string.Join(";", members.Select(m => m.Name)),
                ["HelixBatchManifest"] = "batch-manifest.json"
            });

            Log.LogMessage(
                MessageImportance.High,
                $"Batched {members.Count} Helix work items into '{batchName}': {string.Join(", ", members.Select(m => m.Name))}");
            return batchWorkItem;
        }

        private static string CreatePosixRunner(IReadOnlyList<ManifestEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#!/bin/sh");
            builder.AppendLine("set +e");
            builder.AppendLine("batch_exit=0");
            builder.AppendLine("batch_root=$(pwd)");
            builder.AppendLine("upload_root=${HELIX_WORKITEM_UPLOAD_ROOT:-$batch_root/uploads}");
            builder.AppendLine("mkdir -p \"$upload_root\"");
            for (int i = 0; i < entries.Count; i++)
            {
                string memberId = GetMemberId(i, entries[i].Name);
                string payload = entries[i].PayloadDirectory;
                builder.AppendLine($"echo \"##[group]Starting batched Helix work item {memberId}\"");
                builder.AppendLine($"member_upload=\"$upload_root/{memberId}\"");
                builder.AppendLine("mkdir -p \"$member_upload\"");
                builder.AppendLine("(");
                builder.AppendLine($"  cd \"$batch_root/{payload}\"");
                builder.AppendLine("  export HELIX_WORKITEM_PAYLOAD=$(pwd)");
                builder.AppendLine("  export HELIX_WORKITEM_ROOT=$(pwd)");
                builder.AppendLine("  export HELIX_WORKITEM_UPLOAD_ROOT=\"$member_upload\"");
                builder.AppendLine($"  /bin/sh ./run-member.sh");
                builder.AppendLine($") > \"$member_upload/console.log\" 2>&1");
                builder.AppendLine("member_exit=$?");
                builder.AppendLine($"find \"$batch_root/{payload}\" -maxdepth 5 \\( -iname '*.trx' -o -iname 'testResults.xml' -o -iname 'test-results.xml' -o -iname 'test_results.xml' -o -iname 'junit-results.xml' -o -iname 'junitresults.xml' \\) -exec cp {{}} \"$member_upload/\" \\; 2>/dev/null");
                builder.AppendLine("if [ $member_exit -ne 0 ]; then batch_exit=$member_exit; fi");
                builder.AppendLine("cat \"$member_upload/console.log\"");
                builder.AppendLine($"echo \"##[endgroup]Finished batched Helix work item {memberId} with exit code $member_exit\"");
            }
            builder.AppendLine("exit $batch_exit");
            return builder.ToString();
        }

        private static string CreateWindowsRunner(IReadOnlyList<ManifestEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("@echo off");
            builder.AppendLine("setlocal EnableExtensions");
            builder.AppendLine("set batch_exit=0");
            builder.AppendLine("set batch_root=%CD%");
            builder.AppendLine("set batch_upload_root=%HELIX_WORKITEM_UPLOAD_ROOT%");
            builder.AppendLine("if \"%batch_upload_root%\"==\"\" set batch_upload_root=%batch_root%\\uploads");
            builder.AppendLine("if not exist \"%batch_upload_root%\" mkdir \"%batch_upload_root%\"");
            for (int i = 0; i < entries.Count; i++)
            {
                string memberId = GetMemberId(i, entries[i].Name);
                string payload = entries[i].PayloadDirectory.Replace('/', '\\');
                builder.AppendLine($"echo ##[group]Starting batched Helix work item {memberId}");
                builder.AppendLine($"set member_upload=%batch_upload_root%\\{memberId}");
                builder.AppendLine("if not exist \"%member_upload%\" mkdir \"%member_upload%\"");
                builder.AppendLine("pushd \"%batch_root%\\" + payload + "\"");
                builder.AppendLine("set HELIX_WORKITEM_PAYLOAD=%CD%");
                builder.AppendLine("set HELIX_WORKITEM_ROOT=%CD%");
                builder.AppendLine("set HELIX_WORKITEM_UPLOAD_ROOT=%member_upload%");
                builder.AppendLine("call run-member.cmd > \"%member_upload%\\console.log\" 2>&1");
                builder.AppendLine("set member_exit=%ERRORLEVEL%");
                builder.AppendLine("popd");
                builder.AppendLine("powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-ChildItem -Path '%batch_root%\\" + payload + "' -Recurse -File -Depth 5 -Include *.trx,testResults.xml,test-results.xml,test_results.xml,junit-results.xml,junitresults.xml -ErrorAction SilentlyContinue | Copy-Item -Destination '%member_upload%' -Force -ErrorAction SilentlyContinue\"");
                builder.AppendLine("if not \"%member_exit%\"==\"0\" set batch_exit=%member_exit%");
                builder.AppendLine("type \"%member_upload%\\console.log\"");
                builder.AppendLine($"echo ##[endgroup]Finished batched Helix work item {memberId} with exit code %member_exit%");
            }
            builder.AppendLine("exit /b %batch_exit%");
            return builder.ToString();
        }

        private void WriteMemberCommand(string batchDirectory, string memberDirectoryName, string command)
        {
            string payloadDirectory = Path.Combine(batchDirectory, "payloads", memberDirectoryName);
            string fileName = IsPosixShell ? "run-member.sh" : "run-member.cmd";
            string contents = IsPosixShell
                ? "#!/bin/sh\n" + command + "\n"
                : "@echo off\r\n" + command + "\r\nexit /b %ERRORLEVEL%\r\n";
            File.WriteAllText(Path.Combine(payloadDirectory, fileName), contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private TimeSpan GetExpectedDuration(ITaskItem workItem)
        {
            foreach (string metadataName in new[] { "ExpectedExecutionTime", "HelixExpectedDuration", "EstimatedDuration" })
            {
                string value = workItem.GetMetadata(metadataName);
                if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan duration) && duration > TimeSpan.Zero)
                {
                    return duration;
                }
            }

            return GetTimeout(workItem, TimeSpan.FromMinutes(1));
        }

        private TimeSpan GetTimeout(ITaskItem workItem, TimeSpan fallback)
        {
            string timeout = workItem.GetMetadata(SendHelixJob.MetadataNames.Timeout);
            if (TimeSpan.TryParse(timeout, CultureInfo.InvariantCulture, out TimeSpan parsed) && parsed > TimeSpan.Zero)
            {
                return parsed;
            }

            return fallback;
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, directory)));
            }

            foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, overwrite: true);
            }
        }

        private static string GetMetadataOrItemSpec(ITaskItem item, string metadataName)
        {
            string metadata = item.GetMetadata(metadataName);
            return string.IsNullOrWhiteSpace(metadata) ? item.ItemSpec : metadata;
        }

        private static string GetMemberId(int index, string name) => $"{index + 1:000}_{ShortHash(name)}";

        private static string SanitizeName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            string sanitized = builder.ToString().Trim('_');
            return sanitized.Length > 32 ? sanitized.Substring(0, 32) : sanitized;
        }

        private static string ShortHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value)
                {
                    hash ^= c;
                    hash *= 16777619;
                }

                return hash.ToString("x", CultureInfo.InvariantCulture);
            }
        }

        private sealed class BatchMember
        {
            public BatchMember(ITaskItem workItem, string name, string command, string payloadDirectory, TimeSpan expectedDuration, TimeSpan timeout)
            {
                WorkItem = workItem;
                Name = name;
                Command = command;
                PayloadDirectory = payloadDirectory;
                ExpectedDuration = expectedDuration;
                Timeout = timeout;
            }

            public ITaskItem WorkItem { get; }
            public string Name { get; }
            public string Command { get; }
            public string PayloadDirectory { get; }
            public TimeSpan ExpectedDuration { get; }
            public TimeSpan Timeout { get; }
        }

        private sealed class BatchManifest
        {
            [JsonProperty("version")]
            public int Version { get; set; }

            [JsonProperty("workItems")]
            public List<ManifestEntry> WorkItems { get; set; }
        }

        private sealed class ManifestEntry
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("command")]
            public string Command { get; set; }

            [JsonProperty("payloadDirectory")]
            public string PayloadDirectory { get; set; }

            [JsonProperty("timeout")]
            public string Timeout { get; set; }

            [JsonProperty("expectedDuration")]
            public string ExpectedDuration { get; set; }
        }
    }
}
