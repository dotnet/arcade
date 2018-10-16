// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Maestro.Data.Models
{
    public class Repository
    {
        // 450 is short enough to work well in SQL indexes,
        // and long enough to hold any repository or branch that we need to store.
        public const int RepositoryNameLength = 450;
        public const int BranchNameLength = 450;

        [MaxLength(RepositoryNameLength)]
        public string RepositoryName { get; set; }

        public long InstallationId { get; set; }

        public List<RepositoryBranch> Branches { get; set; }
    }

    public class RepositoryBranch
    {
        [MaxLength(Repository.RepositoryNameLength)]
        public string RepositoryName { get; set; }

        public Repository Repository { get; set; }

        [MaxLength(Repository.BranchNameLength)]
        public string BranchName { get; set; }

        [Column("Policy")]
        public string PolicyString { get; set; }

        [NotMapped]
        public Policy PolicyObject
        {
            get => PolicyString == null ? null : JsonConvert.DeserializeObject<Policy>(PolicyString);
            set => PolicyString = value == null ? null : JsonConvert.SerializeObject(value);
        }

        public class Policy
        {
            public List<MergePolicyDefinition> MergePolicies { get; set; }
        }
    }

    public class RepositoryBranchUpdate
    {
        [MaxLength(Repository.RepositoryNameLength)]
        public string RepositoryName { get; set; }

        [MaxLength(Repository.BranchNameLength)]
        public string BranchName { get; set; }

        public RepositoryBranch RepositoryBranch { get; set; }

        /// <summary>
        ///     <see langword="true" /> if the update succeeded; <see langword="false" /> otherwise.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     A message describing what the subscription was trying to do.
        ///     e.g. 'Updating dependencies from dotnet/coreclr in dotnet/corefx'
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        ///     The error that occured, if any.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     The method that was called.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        ///     The parameters to the called method.
        /// </summary>
        public string Arguments { get; set; }
    }

    public class RepositoryBranchUpdateHistory
    {
        [MaxLength(Repository.RepositoryNameLength)]
        public string RepositoryName { get; set; }

        [MaxLength(Repository.BranchNameLength)]
        public string BranchName { get; set; }

        /// <summary>
        ///     <see langword="true" /> if the update succeeded; <see langword="false" /> otherwise.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     A message describing what the subscription was trying to do.
        ///     e.g. 'Updating dependencies from dotnet/coreclr in dotnet/corefx'
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        ///     The error that occured, if any.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     The method that was called.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        ///     The parameters to the called method.
        /// </summary>
        public string Arguments { get; set; }
    }
}
