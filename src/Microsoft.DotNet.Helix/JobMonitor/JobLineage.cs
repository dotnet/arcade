// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.DotNet.Helix.JobMonitor.Models;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>
    /// The positive stage-attempt number associated with a Helix job.
    /// </summary>
    internal readonly record struct StageAttempt : IComparable<StageAttempt>
    {
        private StageAttempt(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public int CompareTo(StageAttempt other) => Number.CompareTo(other.Number);

        public static StageAttempt? ParseOptional(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                || number < 1)
            {
                throw new FormatException($"Stage attempt '{value}' must be a positive integer.");
            }

            return new StageAttempt(number);
        }
    }

    /// <summary>
    /// Stable identity for one logical stream of Helix work across stage attempts and
    /// resubmissions.
    /// </summary>
    internal readonly struct WorkStreamIdentity : IEquatable<WorkStreamIdentity>
    {
        private readonly string _value;

        private WorkStreamIdentity(string value)
        {
            _value = value;
        }

        public static WorkStreamIdentity FromSubmitter(string submitterJobName, string queueId)
            => new(string.IsNullOrEmpty(queueId)
                ? $"submitter:{submitterJobName}"
                : $"submitter:{submitterJobName}|queue:{queueId}");

        public static WorkStreamIdentity FromHelixJob(string jobName)
            => new($"helix:{jobName}");

        public bool Equals(WorkStreamIdentity other)
            => StringComparer.OrdinalIgnoreCase.Equals(_value, other._value);

        public override bool Equals(object obj)
            => obj is WorkStreamIdentity other && Equals(other);

        public override int GetHashCode()
            => _value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(_value);

        public override string ToString() => _value ?? string.Empty;

        public static bool operator ==(WorkStreamIdentity left, WorkStreamIdentity right)
            => left.Equals(right);

        public static bool operator !=(WorkStreamIdentity left, WorkStreamIdentity right)
            => !left.Equals(right);
    }

    /// <summary>
    /// A Helix job classified within its logical work stream and resubmission lineage.
    /// </summary>
    internal sealed record JobIncarnation(
        HelixJobInfo Job,
        WorkStreamIdentity WorkStream,
        StageAttempt? StageAttempt,
        int LineageDepth);

    /// <summary>
    /// Pure, immutable view of Helix job identity and lineage for a caller-provided snapshot.
    /// </summary>
    internal sealed class JobLineage
    {
        private readonly IReadOnlyList<HelixJobInfo> _jobs;
        private readonly IReadOnlyDictionary<string, HelixJobInfo> _jobByName;

        public JobLineage(IEnumerable<HelixJobInfo> jobs)
        {
            ArgumentNullException.ThrowIfNull(jobs);

            _jobs = [..jobs];
            _jobByName = _jobs
                .GroupBy(job => job.JobName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }

        public JobIncarnation GetIncarnation(HelixJobInfo job)
        {
            ArgumentNullException.ThrowIfNull(job);

            return new JobIncarnation(
                job,
                GetWorkStream(job),
                StageAttempt.ParseOptional(job.StageAttempt),
                GetLineageDepth(job));
        }

        /// <summary>
        /// Returns jobs that are not named as the predecessor of another job in the snapshot.
        /// </summary>
        public IReadOnlyList<JobIncarnation> GetLatestLineageIncarnations()
        {
            var supersededJobNames = new HashSet<string>(
                _jobs.Select(job => job.PreviousHelixJobName)
                    .Where(previousJobName => !string.IsNullOrEmpty(previousJobName)),
                StringComparer.OrdinalIgnoreCase);

            return
            [
                .._jobs
                    .Where(job => !supersededJobNames.Contains(job.JobName))
                    .Select(GetIncarnation)
            ];
        }

        /// <summary>
        /// Returns the latest lineage leaf for each logical work stream, preferring the higher
        /// stage attempt and then the job name when unlinked jobs share a stream.
        /// </summary>
        public IReadOnlyList<JobIncarnation> GetLatestIncarnationsPerStream()
            =>
            [
                ..GetLatestLineageIncarnations()
                    .GroupBy(incarnation => incarnation.WorkStream)
                    .Select(group => group
                        .OrderBy(incarnation => incarnation.StageAttempt)
                        .ThenBy(incarnation => incarnation.Job.JobName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(incarnation => incarnation.Job.JobName, StringComparer.Ordinal)
                        .Last())
            ];

        /// <summary>
        /// Orders the snapshot from oldest to newest, then by stage attempt and job name.
        /// </summary>
        public IReadOnlyList<JobIncarnation> OrderOldToNew()
            =>
            [
                .._jobs
                    .Select(GetIncarnation)
                    .OrderBy(incarnation => incarnation.LineageDepth)
                    .ThenBy(incarnation => incarnation.StageAttempt)
                    .ThenBy(incarnation => incarnation.Job.JobName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(incarnation => incarnation.Job.JobName, StringComparer.Ordinal)
            ];

        public IReadOnlyList<HelixJobInfo> GetDirectSuccessors(HelixJobInfo job)
        {
            ArgumentNullException.ThrowIfNull(job);

            return
            [
                .._jobs.Where(candidate =>
                    !string.IsNullOrEmpty(candidate.PreviousHelixJobName)
                    && StringComparer.OrdinalIgnoreCase.Equals(candidate.PreviousHelixJobName, job.JobName))
            ];
        }

        private WorkStreamIdentity GetWorkStream(HelixJobInfo job)
        {
            if (!string.IsNullOrEmpty(job.SubmitterJobName))
            {
                return WorkStreamIdentity.FromSubmitter(job.SubmitterJobName, job.QueueId);
            }

            HelixJobInfo current = job;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (current is not null
                && !string.IsNullOrEmpty(current.PreviousHelixJobName)
                && visited.Add(current.JobName))
            {
                if (!_jobByName.TryGetValue(current.PreviousHelixJobName, out HelixJobInfo previous))
                {
                    return WorkStreamIdentity.FromHelixJob(current.PreviousHelixJobName);
                }

                if (!string.IsNullOrEmpty(previous.SubmitterJobName))
                {
                    return WorkStreamIdentity.FromSubmitter(previous.SubmitterJobName, previous.QueueId);
                }

                current = previous;
            }

            return WorkStreamIdentity.FromHelixJob(current?.JobName ?? job.JobName);
        }

        private int GetLineageDepth(HelixJobInfo job)
        {
            int depth = 0;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (!string.IsNullOrEmpty(job.PreviousHelixJobName)
                && visited.Add(job.PreviousHelixJobName)
                && _jobByName.TryGetValue(job.PreviousHelixJobName, out job))
            {
                depth++;
            }

            return depth;
        }
    }
}
