namespace Microsoft.DotNet.DarcLib
{
    public class Check
    {
        public Check(CheckStatus status, string name, string url)
        {
            Status = status;
            Name = name;
            Url = url;
        }

        public CheckStatus Status { get; }
        public string Name { get; }
        public string Url { get; }
    }
}