using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class VstsComment
    {
        public VstsComment(List<VstsCommentBody> comments)
        {
            Comments = comments;
        }

        public List<VstsCommentBody> Comments { get; set; }
    }

    public class VstsCommentBody
    {
        public VstsCommentBody(string content)
        {
            Content = content;
        }

        public string Content { get; set; }

        public int CommentType { get; set; } = 1;
    }
}
