// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Analysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Github.IssueLabeler.Helpers
{
    public class DatasetHelper
    {
        public DatasetHelper(DiffHelper diffHelper)
        {
            _diffHelper = diffHelper;
            _folderSb = new StringBuilder();
            _regexForUserMentions = new Regex(@"@[a-zA-Z0-9_//-]+");
        }
        private readonly Regex _regexForUserMentions;
        private readonly StringBuilder _folderSb;
        private readonly DiffHelper _diffHelper;

        /// <summary>
        /// partitions the dataset in inputPath into train, validate and test datapaths
        /// </summary>
        /// <param name="inputPath">path to the input dataset</param>
        /// <param name="trainPath">the output to store the train dataset</param>
        /// <param name="validatePath">the output to store the train dataset</param>
        /// <param name="testPath">the output to store the train dataset</param>
        public void BreakIntoTrainValidateTestDatasets(string inputPath, string trainPath, string validatePath, string testPath)
        {
            DataFrame df = DataFrame.LoadCsv(inputPath, separator: '\t', header: true);

            // have at least 1000 elements
            Debug.Assert(df.RowCount > 1000);

            SplitTrainTest(df, 0.8f, out DataFrame trainDataset, out DataFrame remainingDataframe);
            SaveToFile(trainDataset, trainPath, withHeader: true);

            SplitTrainTest(remainingDataframe, 0.5f, out DataFrame testDataset, out DataFrame validateDataset);
            SaveToFile(testDataset, validatePath, withHeader: true);
            SaveToFile(validateDataset, testPath, withHeader: true);
        }

        private void SaveToFile(DataFrame dataFrame, string output, bool withHeader = true)
        {
            var lines = new List<string>();
            if (withHeader)
            {
                lines.Add(string.Join("\t", dataFrame.Columns.Select(x => x.Name)));
            }
            for (long i = 0; i < dataFrame.RowCount; i++)
            {
                IList<object> row = dataFrame[i];
                // Now enumerate over the rows 
                lines.Add(string.Join("\t", row));
            }
            File.AppendAllLines(output, lines);
        }

        private void SplitTrainTest(DataFrame input, float trainRatio, out DataFrame trainDataframe, out DataFrame testDataframe)
        {
            IEnumerable<int> indices = Enumerable.Range(0, (int)input.RowCount);
            IEnumerable<int> trainIndices = indices.Take((int)(input.RowCount * trainRatio));
            IEnumerable<int> testIndices = indices.TakeLast((int)(input.RowCount * (1 - trainRatio)));
            trainDataframe = input[trainIndices];
            testDataframe = input[testIndices];
        }

        /// <summary>
        /// saves to file a subset containing only PRs when onlyPrs flag is true, saves only issues otherwise.
        /// </summary>
        /// <param name="input">path to the reference dataset</param>
        /// <param name="output">the output to store the new dataset</param>
        public void FilterByIssueOrPr(string input, string output, FilterIssueOrPr filter = FilterIssueOrPr.PrsOnly)
        {
            DataFrame dataFrame = DataFrame.LoadCsv(input, separator: '\t', header: true);
            switch (filter)
            {
                case FilterIssueOrPr.PrsOnly:
                    dataFrame = dataFrame.Filter(dataFrame["IsPR"].ElementwiseEquals(1));
                    break;
                case FilterIssueOrPr.IssuesOnly:
                    dataFrame = dataFrame.Filter(dataFrame["IsPR"].ElementwiseEquals(0));
                    break;
                case FilterIssueOrPr.Both:
                default:
                    // keep both
                    break;
            }
            SaveToFile(dataFrame, output, withHeader: true);
        }

        public enum FilterIssueOrPr
        {
            PrsOnly = 0,
            IssuesOnly = 1,
            Both = 2
        }

        /// <summary>
        /// saves to file a dataset ready for training, given one created using GithubIssueDownloader.
        /// For training we can remove ID column, and further expand information in FilePaths
        /// We also retrieve user @ mentions from instead Description and add into new columns
        /// </summary>
        /// <param name="input">path to the reference dataset</param>
        /// <param name="output">the output to store the new dataset</param>
        /// <param name="includeFileColumns">when true, it contains extra columns with file related information</param>
        public void AddOrRemoveColumnsPriorToTraining(string input, string output, bool includeFileColumns = true)
        {
            DataFrame dataFrame = DataFrame.LoadCsv(input, separator: '\t', header: true);
            var existingHeaderNames = new string[] { "ID", "Area", "Title", "Description", "IsPR", "FilePaths" };
            Debug.Assert(dataFrame.Columns.Select(x => x.Name).Intersect(existingHeaderNames).Equals(existingHeaderNames));
            dataFrame.Columns.Remove("ID");
            var filePathsColumn = dataFrame.Columns.Where(x => x.Name.Equals("FilePaths", StringComparison.OrdinalIgnoreCase));

            var newColumnsToAdd = new List<(string, StringDataFrameColumn)>();
            foreach (var item in new string[] { "NumMentions", "UserMentions" })
            {
                dataFrame.Columns.Add(new StringDataFrameColumn(item, dataFrame.RowCount));
            }

            if (includeFileColumns)
            {
                foreach (var item in new string[] { "FileCount", "Files", "Filenames", "FileExtensions", "FolderNames", "Folders" })
                {
                    dataFrame.Columns.Add(new StringDataFrameColumn(item, dataFrame.RowCount));
                }
            }

            int bodyIndex = dataFrame.Columns.IndexOf("Description");
            int numMentionsIndex = dataFrame.Columns.IndexOf("NumMentions");
            int userMentionsIndex = dataFrame.Columns.IndexOf("UserMentions");
            int filePathsIndex = dataFrame.Columns.IndexOf("FilePaths");
            int fileCountIndex = dataFrame.Columns.IndexOf("FileCount");
            int filesIndex = dataFrame.Columns.IndexOf("Files");
            int filenamesIndex = dataFrame.Columns.IndexOf("Filenames");
            int fileExtensionsIndex = dataFrame.Columns.IndexOf("FileExtensions");
            int folderNamesIndex = dataFrame.Columns.IndexOf("FolderNames");
            int foldersIndex = dataFrame.Columns.IndexOf("Folders");
            string body;
            for (long i = 0; i < dataFrame.RowCount; i++)
            {
                IList<object> row = dataFrame[i];

                body = dataFrame.Columns[bodyIndex][i].ToString();
                dataFrame.Columns[bodyIndex][i] = body.Replace('"', '`');

                var userMentions = _regexForUserMentions.Matches(body).Select(x => x.Value).ToArray();
                dataFrame.Columns[numMentionsIndex][i] = userMentions.Length;
                dataFrame.Columns[userMentionsIndex][i] = FlattenIntoColumn(userMentions);
                if (includeFileColumns)
                {
                    string[] filePaths = row[filePathsIndex].ToString().Split(';');
                    int numFilesChanged = filePaths.Length == 1 && string.IsNullOrEmpty(filePaths[0]) ? 0 : filePaths.Length;
                    _diffHelper.ResetTo(filePaths);
                    dataFrame.Columns[fileCountIndex][i] = numFilesChanged;
                    dataFrame.Columns[filesIndex][i] = FlattenIntoColumn(filePaths);
                    dataFrame.Columns[filenamesIndex][i] = FlattenIntoColumn(_diffHelper.Filenames);
                    dataFrame.Columns[fileExtensionsIndex][i] = FlattenIntoColumn(_diffHelper.Extensions);
                    dataFrame.Columns[folderNamesIndex][i] = FlattenIntoColumn(_diffHelper.FolderNames);
                    dataFrame.Columns[foldersIndex][i] = FlattenIntoColumn(_diffHelper.Folders);
                }
                else
                {
                    dataFrame.Columns[fileCountIndex][i] = 0;
                    dataFrame.Columns[filesIndex][i] = string.Empty;
                    dataFrame.Columns[filenamesIndex][i] = string.Empty;
                    dataFrame.Columns[fileExtensionsIndex][i] = string.Empty;
                    dataFrame.Columns[folderNamesIndex][i] = string.Empty;
                    dataFrame.Columns[foldersIndex][i] = string.Empty;
                }
            }
            SaveToFile(dataFrame, output, withHeader: true);
        }

        /// <summary>
        /// flattens a dictionary to be repeated in a space separated format
        /// </summary>
        /// <param name="textToFlatten">a dictionary containing text and number of times they were repeated</param>
        /// <returns>space delimited text</returns>
        public string FlattenIntoColumn(Dictionary<string, int> folder)
        {
            _folderSb.Clear();
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

        /// <summary>
        /// flattens texts in a space separated format
        /// </summary>
        /// <param name="array">the input containing text to show</param>
        /// <returns>space delimited text</returns>
        public string FlattenIntoColumn(string[] array)
        {
            return string.Join(' ', array);
        }

        /// <summary>
        /// flattens texts in a space separated format
        /// </summary>
        /// <param name="enumerable">the input containing text to show</param>
        /// <returns>space delimited text</returns>
        public string FlattenIntoColumn(IEnumerable<string> enumerable)
        {
            return string.Join(' ', enumerable);
        }
    }
}
