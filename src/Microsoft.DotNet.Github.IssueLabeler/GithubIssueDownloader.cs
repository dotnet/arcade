// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Octokit;
using System;
using System.IO;
using System.Linq;
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
            File.WriteAllText(_outputFile, "ID\tArea\tTitle\tDescription\tIsPR\tFilePaths" + Environment.NewLine);
            for (int i = _startIndex; i < _endIndex; i++)
            {
                try
                {
                    string filePaths = string.Empty;
                    bool isPr = true;
                    try
                    {
                        var prFiles = await _client.PullRequest.Files(_owner, _repoName, i);
                        filePaths = String.Join(";", prFiles.Select(x => x.FileName));
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("files was not found."))
                            isPr = false;
                        else
                            throw ex;
                    }

                    var issue = await _client.Issue.Get(_owner, _repoName, i);

                    foreach (var label in issue.Labels)
                    {
                        if (label.Name.Contains("area-"))
                        {
                            string title = RemoveNewLineCharacters(issue.Title);
                            string description = RemoveNewLineCharacters(issue.Body);
                            // Ordering is important here because we are using the same ordering on the prediction side.
                            sb.AppendLine($"{i}\t{label.Name}\t\"{title}\"\t\"{description}\"\t{isPr}\t{filePaths}");
                        }
                    }

                    if (i % 1000 == 0)
                    {
                        File.AppendAllText(_outputFile, sb.ToString());
                        sb.Clear();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Issue {i}: " + ex.Message);
                }
            }

            File.AppendAllText(_outputFile, sb.ToString());
        }

        private static string RemoveNewLineCharacters(string input)
        {
            return input.Replace("\r\n", " ").Replace("\n", " ").Replace("\t", " ");
        }
    }
}
