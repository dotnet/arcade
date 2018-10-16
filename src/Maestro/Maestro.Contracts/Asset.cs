using System.Runtime.Serialization;

namespace Maestro.Contracts
{
    [DataContract]
    public class Asset
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Version { get; set; }
    }
}
