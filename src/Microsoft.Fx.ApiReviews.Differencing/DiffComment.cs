// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffComment
    {
        public DiffComment(string docId, string author, string text)
        {
            DocId = docId;
            Author = author;
            Text = text;
        }

        public string DocId { get; private set; }
        public string Author { get; private set; }
        public string Text { get; private set; }

    }
}
