// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    public class AzureDevOpsChange
    {
        public AzureDevOpsChange(string filePath, string content, string contentType = null)
        {
            Item = new Item(filePath);
            NewContent = new NewContent(content, contentType);
            ChangeType = AzureDevOpsChangeType.Edit;
        }

        public int ChangeType { get; set; }

        public Item Item { get; set; }

        public NewContent NewContent { get; set; }
    }

    public class Item
    {
        public Item(string path)
        {
            Path = path;
        }

        public string Path { get; set; }
    }

    public class NewContent
    {
        public NewContent(string content, string contentType = null)
        {
            Content = content;

            if (!string.IsNullOrEmpty(contentType))
            {
                ContentType = contentType;
            }
        }

        public string Content { get; set; }

        public string ContentType { get; set; } = "rawtext";
    }
}
