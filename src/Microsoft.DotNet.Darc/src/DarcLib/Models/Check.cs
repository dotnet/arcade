namespace Microsoft.DotNet.DarcLib
{
    public class Check
    {
        public Check(CheckState status, string name, string url)
        {
            Status = status;
            Name = name;
            Url = url;
        }

        public CheckState Status { get; }
        public string Name { get; }
        public string Url { get; }
    }
}