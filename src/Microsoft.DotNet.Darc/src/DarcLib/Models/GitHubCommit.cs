using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Darc
{
    public class GitHubCommit
    {
        public GitHubCommit(string message, string content, string branch)
        {
            Message = message;
            Content = content;
            Branch = branch;
        }

        public string Message { get; set; }

        public string Content { get; set; }

        public string Branch { get; set; }

        public string Sha { get; set; }
    }
}
