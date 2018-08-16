// <auto-generated>
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// </auto-generated>

namespace Microsoft.DotNet.Maestro.Client.Models
{
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using System.Linq;

    public partial class DefaultChannel
    {
        /// <summary>
        /// Initializes a new instance of the DefaultChannel class.
        /// </summary>
        public DefaultChannel()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the DefaultChannel class.
        /// </summary>
        public DefaultChannel(string repository, int? id = default(int?), string branch = default(string), Channel channel = default(Channel))
        {
            Id = id;
            Repository = repository;
            Branch = branch;
            Channel = channel;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public int? Id { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "repository")]
        public string Repository { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "branch")]
        public string Branch { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "channel")]
        public Channel Channel { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (Repository == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "Repository");
            }
            if (Repository != null)
            {
                if (Repository.Length > 300)
                {
                    throw new ValidationException(ValidationRules.MaxLength, "Repository", 300);
                }
                if (Repository.Length < 0)
                {
                    throw new ValidationException(ValidationRules.MinLength, "Repository", 0);
                }
            }
            if (Branch != null)
            {
                if (Branch.Length > 100)
                {
                    throw new ValidationException(ValidationRules.MaxLength, "Branch", 100);
                }
                if (Branch.Length < 0)
                {
                    throw new ValidationException(ValidationRules.MinLength, "Branch", 0);
                }
            }
        }
    }
}
