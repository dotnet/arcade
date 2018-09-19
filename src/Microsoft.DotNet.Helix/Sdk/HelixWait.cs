using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class HelixWait : HelixTask
    {
        /// <summary>
        /// 
        /// </summary>
        [Required]
        public string[] JobIds { get; set; }

        protected override Task ExecuteCore()
        {
            throw new NotImplementedException();
        }
    }
}
