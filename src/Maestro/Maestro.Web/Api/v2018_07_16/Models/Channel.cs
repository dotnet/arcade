using System;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class Channel
    {
        public Channel(Data.Models.Channel other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Id = other.Id;
            Name = other.Name;
            Classification = other.Classification;
        }

        public int Id { get; }

        public string Name { get; }

        public string Classification { get; }
    }
}
