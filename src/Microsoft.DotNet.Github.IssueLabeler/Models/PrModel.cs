// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML.Data;
using System;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class PrModel : IssueModel
    {
        [LoadColumn(6)]
        public Single FileCount;

        [LoadColumn(7)]
        public string Files;

        [LoadColumn(8)]
        public string Filenames;

        [LoadColumn(9)]
        public string FileExtensions;

        [LoadColumn(10)]
        public string FolderNames;

        [LoadColumn(11)]
        public string Folders;
    }
}
