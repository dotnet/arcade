namespace Maestro
{
    public class Repository
    {
        public Repository(string uri, string branch)
        {
            Uri = uri;
            Branch = branch;
        }

        public string Uri { get; }
        public string Branch { get; }
    }
}
