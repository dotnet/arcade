namespace Microsoft.DotNet.DarcLib
{
    public class Commit
    {
        public Commit(string author, string sha)
        {
            Author = author;
            Sha = sha;
        }

        public string Author { get; }
        public string Sha { get; }
    }
}