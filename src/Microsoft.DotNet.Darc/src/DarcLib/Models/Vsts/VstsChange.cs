namespace Microsoft.DotNet.Darc
{
    public class VstsChange
    {
        public VstsChange(string filePath, string content, string contentType = null)
        {
            Item = new Item(filePath);
            NewContent = new NewContent(content, contentType);
            ChangeType = VstsChangeType.Edit;
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
