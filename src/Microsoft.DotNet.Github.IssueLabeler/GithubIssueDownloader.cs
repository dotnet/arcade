// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Octokit;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Github.IssueLabeler
{
    public class GithubIssueDownloader
    {
        private GitHubClient _client;
        public string _repoName;
        private string _owner;
        private int _startIndex;
        private int _endIndex;
        private string _outputFile;
  
        public GithubIssueDownloader(string authToken, string repoName, string owner, int startIndex, int endIndex, string OutputFile)
        {
            if (startIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (endIndex <= 0 || endIndex < startIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            }

            _client = new GitHubClient(new ProductHeaderValue("GithubIssueDownloader"))
            {
                Credentials = new Credentials(authToken)
            };

            _repoName = repoName;
            _owner = owner;
            _startIndex = startIndex;
            _endIndex = endIndex;
            _outputFile = OutputFile;
        }

        public async Task DownloadAndSaveAsync()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = _startIndex; i < _endIndex; i++)
            {
                try
                {
                    var issue = await _client.Issue.Get(_owner, _repoName, i);

                    foreach (var label in issue.Labels)
                    {
                        if (label.Name.Contains("area-"))
                        {
                            string title = RemoveNewLineCharacters(issue.Title);
                            string body = RemoveNewLineCharacters(issue.Body);
                            sb.AppendLine($"{issue.Number}\t\"{title}\"\t\"{body}\"\t{label.Name}");
                        }
                    }

                    if (i % 1000 == 0)
                    {
                        File.AppendAllText(_outputFile, sb.ToString());
                        sb.Clear();
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"Issue {i} does not exist");
                }
            }

            File.AppendAllText(_outputFile, sb.ToString());
        }

        private static string RemoveNewLineCharacters(string input)
        {
            return input.Replace("\r\n", " ").Replace("\n", " ");
        }
    }
}
