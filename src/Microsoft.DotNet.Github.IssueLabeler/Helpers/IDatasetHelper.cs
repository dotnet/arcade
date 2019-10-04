// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Github.IssueLabeler.Helpers
{
    public interface IDatasetHelper
    {
        /// <summary>
        /// partitions the dataset in inputPath into train, validate and test datapaths
        /// </summary>
        /// <param name="inputPath">path to the input dataset</param>
        /// <param name="trainPath">the output to store the train dataset</param>
        /// <param name="validatePath">the output to store the train dataset</param>
        /// <param name="testPath">the output to store the train dataset</param>
        void BreakIntoTrainValidateTestDatasets(string inputPath, string trainPath, string validatePath, string testPath);

        /// <summary>
        /// saves to file a subset containing only PRs
        /// </summary>
        /// <param name="input">path to the reference dataset</param>
        /// <param name="output">the output to store the new dataset</param>
        void OnlyPrs(string input, string output);

        /// <summary>
        /// saves to file a subset containing only issues
        /// </summary>
        /// <param name="input">path to the reference dataset</param>
        /// <param name="output">the output to store the new dataset</param>
        void OnlyIssues(string input, string output);

        /// <summary>
        /// saves to file a dataset ready for training, given one created using GithubIssueDownloader.
        /// For training we can remove ID column, and further expand information in FilePaths
        /// We also retrieve user @ mentions from instead Description and add into new columns
        /// </summary>
        /// <param name="input">path to the reference dataset</param>
        /// <param name="output">the output to store the new dataset</param>
        /// <param name="includeFileColumns">when true, it contains extra columns with file related information</param>
        void AddOrRemoveColumnsPriorToTraining(string input, string output, bool includeFileColumns = true);

        /// <summary>
        /// flattens a dictionary to be repeated in a space separated format
        /// </summary>
        /// <param name="textToFlatten">a dictionary containing text and number of times they were repeated</param>
        /// <returns>space delimited text</returns>
        string FlattenIntoColumn(Dictionary<string, int> textToFlatten);

        /// <summary>
        /// flattens texts in a space separated format
        /// </summary>
        /// <param name="array">the input containing text to show</param>
        /// <returns>space delimited text</returns>
        string FlattenIntoColumn(string[] array);

        /// <summary>
        /// flattens texts in a space separated format
        /// </summary>
        /// <param name="enumerable">the input containing text to show</param>
        /// <returns>space delimited text</returns>
        string FlattenIntoColumn(IEnumerable<string> enumerable);
    }

    internal class DatasetHelper : IDatasetHelper
    {
        private Regex _regex;
        private StringBuilder _sb;
        private StringBuilder _folderSb;
        private IDiffHelper _diffHelper;

        public void BreakIntoTrainValidateTestDatasets(string inputPath, string trainPath, string validatePath, string testPath)
        {
            var lines = File.ReadAllLines(inputPath);
            int totalCount = lines.Length;

            // have at least 1000 elements
            Debug.Assert(totalCount > 1000);
            int numInTrain = (int)(lines.Length * 0.8);
            int numInValidate = (int)(lines.Length * 0.1);

            // 80% into train dataset
            SaveFromXToY(
                inputPath,
                trainPath,
                numToSkip: 0, length: numInTrain); 

            // next 10% into validate dataset
            SaveFromXToY(
                inputPath,
                validatePath,
                numToSkip: numInTrain, length: numInValidate); // next 10%

            // remaining 10% into test dataset
            SaveFromXToY(
                inputPath,
                testPath,
                numToSkip: numInTrain + numInValidate);
        }

        private void SaveFromXToY(string input, string output, int numToSkip, int length = -1)
        {
            var lines = File.ReadAllLines(input);
            var span = lines.AsSpan();
            var header = span.Slice(0, 1).ToArray(); // include header
            File.WriteAllLines(output, header);
            span = span.Slice(numToSkip + 1, span.Length - (numToSkip + 1));
            if (length != -1)
            {
                span = span.Slice(0, length); // include header
            }
            lines = span.ToArray();
            File.AppendAllLines(output, lines);
        }

        public void OnlyPrs(string input, string output)
        {
            var lines = File.ReadAllLines(input);
            var span = lines.AsSpan();
            var header = span.Slice(0, 1).ToArray(); // include header
            Debug.Assert(header[0].Split("\t")[3] == "IsPR");
            File.WriteAllLines(output, header);
            span = span.Slice(1, span.Length - 1);
            lines = span.ToArray();
            File.AppendAllLines(output, lines.Where(x => int.TryParse(x.Split('\t')[3], out int isPrAsNumber) && isPrAsNumber == 1).ToArray());
        }

        public void OnlyIssues(string input, string output)
        {
            var lines = File.ReadAllLines(input);
            var span = lines.AsSpan();
            var header = span.Slice(0, 1).ToArray(); // include header
            Debug.Assert(header[0].Split("\t")[3] == "IsPR");
            File.WriteAllLines(output, header);
            span = span.Slice(1, span.Length - 1);
            lines = span.ToArray();
            File.AppendAllLines(output, lines.Where(x => int.TryParse(x.Split('\t')[3], out int isPrAsNumber) && isPrAsNumber == 0).ToArray());
        }

        public void AddOrRemoveColumnsPriorToTraining(string input, string output, bool includeFileColumns = true)
        {
            if (_sb == null)
            {
                _sb = new StringBuilder();
            }
            var lines = File.ReadAllLines(input);
            Debug.Assert(lines[0].Equals("ID\tArea\tTitle\tDescription\tIsPR\tFilePaths", StringComparison.OrdinalIgnoreCase));
            string newHeader = "Area\tTitle\tDescription\tIsPR\tNumMentions\tUserMentions";
            if (includeFileColumns)
            {
                newHeader += "\tFileCount\tFiles\tFilenames\tFileExtensions\tFolderNames\tFolders";
            }
            string[] newLines = new string[lines.Length];
            newLines[0] = newHeader;
            string line; // current line
            string area, body, title;
            for (int i = 1; i < lines.Length; i++) // skipping header
            {
                _sb.Clear();
                line = lines[i];
                string[] lineSplitByTab = line.Split('\t');
                Debug.Assert(int.TryParse(lineSplitByTab[0], out int _)); // skip ID
                area = lineSplitByTab[1];
                title = lineSplitByTab[2];
                body = lineSplitByTab[3];
                Debug.Assert(int.TryParse(lineSplitByTab[4], out int isPrAsNumber) && (isPrAsNumber == 1 || isPrAsNumber == 0));
                _sb.Append(area)
                    .Append('\t').Append(title)
                    .Append('\t').Append(body)
                    .Append('\t').Append(isPrAsNumber);

                AppendColumnsForUserMentions(body);
                if (includeFileColumns)
                {
                    AppendColumnsForFileDiffs(lineSplitByTab[5], isPr: isPrAsNumber == 1);
                }
                newLines[i] = _sb.ToString().Replace('"', '`');
            }
            File.WriteAllLines(output, newLines);
        }

        private void AppendColumnsForUserMentions(string body)
        {
            if (_regex == null)
            {
                _regex = new Regex(@"@[a-zA-Z0-9_//-]+");
            }
            var userMentions = _regex.Matches(body).Select(x => x.Value).ToArray();
            _sb.Append('\t').Append(userMentions.Length)
                .Append('\t').Append(FlattenIntoColumn(userMentions));
        }

        private void AppendColumnsForFileDiffs(string semicolonDelimitedFilesWithDiff, bool isPr)
        {
            if (isPr)
            {
                string[] filePaths = semicolonDelimitedFilesWithDiff.Split(';');
                int numFilesChanged = filePaths.Length == 1 && string.IsNullOrEmpty(filePaths[0]) ? 0 : filePaths.Length;
                _sb.Append('\t').Append(numFilesChanged);
                if (numFilesChanged != 0)
                {
                    if (_diffHelper == null)
                    {
                        _diffHelper = new DiffHelper(filePaths);
                    }
                    else
                    {
                        _diffHelper.ResetTo(filePaths);
                    }
                    _sb.Append('\t').Append(FlattenIntoColumn(filePaths))
                        .Append('\t').Append(FlattenIntoColumn(_diffHelper.Filenames))
                        .Append('\t').Append(FlattenIntoColumn(_diffHelper.Extensions))
                        .Append('\t').Append(FlattenIntoColumn(_diffHelper.FolderNames))
                        .Append('\t').Append(FlattenIntoColumn(_diffHelper.Folders));
                }
                else
                {
                    _sb.Append('\t', 5);
                }
            }
            else
            {
                _sb.Append('\t').Append(0)
                    .Append('\t', 5);
            }
        }

        public string FlattenIntoColumn(Dictionary<string, int> folder)
        {
            if (_folderSb == null)
            {
                _folderSb = new StringBuilder();
            }
            else
            {
                _folderSb.Clear();
            }
            string res;
            foreach (var f in folder.OrderByDescending(x => x.Value))
            {
                Debug.Assert(f.Value >= 1);
                _folderSb.Append(f.Key);
                for (int j = 0; j < f.Value - 1; j++)
                {
                    _folderSb.Append(" ").Append(f.Key);
                }
                _folderSb.Append(" ");
            }
            if (_folderSb.Length == 0)
            {
                res = string.Empty;
            }
            else
            {
                res = _folderSb.ToString();
                res = res.Substring(0, res.Length - 1);
            }
            return res;
        }

        public string FlattenIntoColumn(string[] array)
        {
            return string.Join(' ', array);
        }

        public string FlattenIntoColumn(IEnumerable<string> enumerable)
        {
            return string.Join(' ', enumerable);
        }
    }
}
