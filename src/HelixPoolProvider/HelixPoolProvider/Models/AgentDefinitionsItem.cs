using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentDefinitionsItem
    {
        public string count { get { return value.Count.ToString(); } }
        public List<AgentDefinitionItem> value;
    }
}
